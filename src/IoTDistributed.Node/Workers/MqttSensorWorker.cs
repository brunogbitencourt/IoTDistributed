
using IoTDistributed.Node.Persistence;
using MQTTnet;
using System.Text.Json;
using IoTDistributed.Node.Services;
using IoTDistributed.Node.Sensors;

namespace IoTDistributed.Node.Workers;

public class MqttSensorWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MqttSensorWorker> _logger;

    public MqttSensorWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<MqttSensorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        if (nodeId <= 0)
            throw new InvalidOperationException("NODE_ID inválido.");

        string host = _configuration["MQTT:Host"] ?? "emqx";
        int port = _configuration.GetValue("MQTT:Port", 1883);

        var factory = new MqttClientFactory();
        using var client = factory.CreateMqttClient();

        var options = new MqttClientOptionsBuilder()
            .WithClientId($"iotdistributed-node-{nodeId}")
            .WithTcpServer(host, port)
            .Build();


        client.ApplicationMessageReceivedAsync += async args =>
        {
            string topic = args.ApplicationMessage.Topic;
            string payload =
                args.ApplicationMessage.ConvertPayloadToString();

            try
            {
                _logger.LogInformation(
                    "NODE {NodeId}: MQTT recebido no tópico {Topic}",
                    nodeId,
                    topic);

                MqttSensorPayload? message =
                    JsonSerializer.Deserialize<MqttSensorPayload>(payload);

                if (message?.EventId is null ||
                    message.EventId == Guid.Empty ||
                    string.IsNullOrWhiteSpace(message.SensorId) ||
                    message.Value is null ||
                    message.CollectedAtUtc is null)
                {
                    throw new ArgumentException(
                        "Payload MQTT incompleto ou inválido.");
                }

                var reading = new SensorReading(
                    EventId: message.EventId.Value,
                    SensorId: message.SensorId,
                    Value: message.Value.Value,
                    CollectedAtUtc:
                        message.CollectedAtUtc.Value.UtcDateTime,
                    OriginNodeId: nodeId
                );

                await using var scope =
                    _scopeFactory.CreateAsyncScope();

                var ingestionService =
                    scope.ServiceProvider
                        .GetRequiredService<SensorIngestionService>();

                bool inserted = await ingestionService.ProcessAsync(
                    reading,
                    mqttTopic: topic,
                    cancellationToken: stoppingToken);

                _logger.LogInformation(
                    "NODE {NodeId}: MQTT processado. Evento={EventId}, Inserido={Inserted}",
                    nodeId,
                    reading.EventId,
                    inserted);
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                // Aplicação encerrando.
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "NODE {NodeId}: erro ao processar MQTT do tópico {Topic}",
                    nodeId,
                    topic);
            }
        };


        bool subscriptionsReady = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!client.IsConnected)
                {
                    await client.ConnectAsync(options, stoppingToken);

                    subscriptionsReady = false;

                    _logger.LogInformation(
                        "NODE {NodeId}: conectado ao MQTT em {Host}:{Port}",
                        nodeId,
                        host,
                        port);
                }

                if (!subscriptionsReady)
                {
                    await using var scope =
                        _scopeFactory.CreateAsyncScope();

                    var repository =
                        scope.ServiceProvider
                            .GetRequiredService<SensorRepository>();

                    var topics = await repository.GetMqttTopicsByNodeAsync(
                        nodeId,
                        stoppingToken);

                    foreach (string topic in topics)
                    {
                        var subscribeOptions = factory
                            .CreateSubscribeOptionsBuilder()
                            .WithTopicFilter(t => t
                                .WithTopic(topic)
                                .WithAtLeastOnceQoS())
                            .Build();

                        await client.SubscribeAsync(
                            subscribeOptions,
                            stoppingToken);

                        _logger.LogInformation(
                            "NODE {NodeId}: inscrito no tópico {Topic}",
                            nodeId,
                            topic);
                    }

                    subscriptionsReady = true;
                }
            }
            catch (OperationCanceledException)
                when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "NODE {NodeId}: falha na conexão/assinatura MQTT",
                    nodeId);
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(5),
                    stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
