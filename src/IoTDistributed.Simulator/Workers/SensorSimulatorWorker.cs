using System.Globalization;
using System.Text.Json;
using IoTDistributed.Simulator.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Protocol;

namespace IoTDistributed.Simulator.Workers;

/// <summary>
/// Publicador MQTT opcional para demonstrações sem o ESP32 físico.
/// Não acessa banco, não chama gRPC e não altera os serviços dos nós.
/// </summary>
public sealed class SensorSimulatorWorker : BackgroundService
{
    private readonly ILogger<SensorSimulatorWorker> _logger;
    private readonly string _host;
    private readonly int _port;
    private readonly TimeSpan _interval;
    private readonly string? _username;
    private readonly string? _password;

    public SensorSimulatorWorker(
        IConfiguration configuration,
        ILogger<SensorSimulatorWorker> logger)
    {
        _logger = logger;
        _host = configuration["MQTT:Host"] ?? "emqx";
        _port = configuration.GetValue("MQTT:Port", 1883);
        _username = configuration["MQTT:Username"];
        _password = configuration["MQTT:Password"];

        int intervalSeconds = configuration.GetValue("Simulator:IntervalSeconds", 5);

        if (string.IsNullOrWhiteSpace(_host))
            throw new ArgumentException("MQTT:Host não pode ser vazio.");

        if (_port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(configuration),
                "MQTT:Port deve estar entre 1 e 65535.");

        if (intervalSeconds is < 1 or > 3600)
            throw new ArgumentOutOfRangeException(nameof(configuration),
                "Simulator:IntervalSeconds deve estar entre 1 e 3600.");

        _interval = TimeSpan.FromSeconds(intervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        // Client ID diferente do ESP32 (iot-sim-...) e dos nós
        // (iotdistributed-node-N). Evita desconectar qualquer cliente real.
        var clientId = $"iot-dotnet-demo-{Guid.NewGuid():N}";

        var optionsBuilder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(_host, _port);

        if (!string.IsNullOrWhiteSpace(_username))
            optionsBuilder.WithCredentials(_username, _password);

        var options = optionsBuilder.Build();

        _logger.LogInformation(
            "Simulador iniciado. Broker={Host}:{Port}, ClientId={ClientId}, " +
            "Intervalo={IntervalSeconds}s, Sensores={SensorCount}",
            _host, _port, clientId, _interval.TotalSeconds,
            SensorDefinition.All.Count);

        // A primeira publicação acontece após o intervalo, permitindo
        // que os nós concluam suas inscrições no broker.
        using var timer = new PeriodicTimer(_interval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    if (!client.IsConnected)
                    {
                        await client.ConnectAsync(options, stoppingToken);

                        _logger.LogInformation(
                            "Simulador conectado ao EMQX em {Host}:{Port}",
                            _host, _port);
                    }

                    // Cada lote contém três GUIDs independentes, um por sensor.
                    foreach (var sensor in SensorDefinition.All)
                    {
                        stoppingToken.ThrowIfCancellationRequested();

                        var value = Math.Round(
                            sensor.Minimum +
                            Random.Shared.NextDouble() *
                            (sensor.Maximum - sensor.Minimum),
                            2,
                            MidpointRounding.AwayFromZero);

                        var payload = new SensorMessage(
                            Guid.NewGuid(),
                            sensor.SensorId,
                            value,
                            DateTimeOffset.UtcNow.ToString(
                                "yyyy-MM-dd'T'HH:mm:ss'Z'",
                                CultureInfo.InvariantCulture));

                        var message = new MqttApplicationMessageBuilder()
                            .WithTopic(sensor.Topic)
                            .WithPayload(JsonSerializer.Serialize(payload))
                            .WithQualityOfServiceLevel(
                                MqttQualityOfServiceLevel.AtLeastOnce)
                            // Retained=false por padrão: os dados são eventos,
                            // não o último estado persistente do tópico.
                            .Build();

                        await client.PublishAsync(message, stoppingToken);

                        _logger.LogInformation(
                            "MQTT publicado: Topic={Topic}, Sensor={SensorId}, " +
                            "Value={Value:F2}, EventId={EventId}",
                            sensor.Topic, sensor.SensorId, value, payload.EventId);
                    }
                }
                catch (OperationCanceledException)
                    when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Uma queda do broker não encerra o Worker.
                    // Na próxima iteração tentamos conectar novamente.
                    _logger.LogWarning(
                        ex,
                        "Falha ao publicar MQTT. Nova tentativa no próximo ciclo " +
                        "de {IntervalSeconds}s.",
                        _interval.TotalSeconds);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Encerramento normal do contêiner.
        }

        _logger.LogInformation("Simulador MQTT encerrado.");
    }
}
