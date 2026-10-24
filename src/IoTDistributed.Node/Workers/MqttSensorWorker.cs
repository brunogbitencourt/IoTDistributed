
using IoTDistributed.Node.Persistence;
using MQTTnet;

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

        // Chamado sempre que uma publicação chega ao cliente.
        client.ApplicationMessageReceivedAsync += args =>
        {
            string topic = args.ApplicationMessage.Topic;
            string? payload =
                args.ApplicationMessage.ConvertPayloadToString();

            _logger.LogInformation(
                "NODE {NodeId}: MQTT recebido. Tópico={Topic}, Payload={Payload}",
                nodeId,
                topic,
                payload);

            return Task.CompletedTask;
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
