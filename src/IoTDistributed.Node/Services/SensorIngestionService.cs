using IoTDistributed.Node.Persistence;
using IoTDistributed.Node.Sensors;

namespace IoTDistributed.Node.Services;

public class SensorIngestionService
{
    private readonly SensorRepository _repository;
    private readonly ReplicationService _replicationService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SensorIngestionService> _logger;

    public SensorIngestionService(
        SensorRepository repository,
        ReplicationService replicationService,
        IConfiguration configuration,
        ILogger<SensorIngestionService> logger)
    {
        _repository = repository;
        _replicationService = replicationService;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(
        SensorReading reading,
        string? mqttTopic = null,
        CancellationToken cancellationToken = default)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        if (nodeId <= 0)
            throw new InvalidOperationException(
                "NODE_ID não configurado.");

        if (reading.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(reading.SensorId) ||
            !double.IsFinite(reading.Value) ||
            reading.CollectedAtUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException(
                "Dados da medição inválidos.");
        }

        if (reading.OriginNodeId != nodeId)
            throw new ArgumentException(
                "Origem da medição inválida.");

        int? ownerNodeId = await _repository.GetOwnerNodeIdAsync(
            reading.SensorId,
            cancellationToken);

        if (ownerNodeId is null)
            throw new KeyNotFoundException(
                "Sensor não cadastrado.");

        if (ownerNodeId != nodeId)
            throw new InvalidOperationException(
                $"Sensor pertence ao NODE {ownerNodeId}.");

        // Validação adicional para mensagens recebidas via MQTT.
        if (mqttTopic is not null)
        {
            string? registeredTopic =
                await _repository.GetMqttTopicAsync(
                    reading.SensorId,
                    cancellationToken);

            if (registeredTopic != mqttTopic)
                throw new ArgumentException(
                    "Tópico MQTT não corresponde ao sensor.");
        }

        bool inserted = await _repository.SaveAsync(
            reading,
            cancellationToken);

        if (inserted)
        {
            await _replicationService.ReplicateAsync(
                reading,
                cancellationToken);
        }

        _logger.LogInformation(
            "NODE {NodeId}: sensor {SensorId}, evento {EventId}, inserido: {Inserted}",
            nodeId,
            reading.SensorId,
            reading.EventId,
            inserted);

        return inserted;
    }
}
