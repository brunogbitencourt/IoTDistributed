
using IoTDistributed.Node.Persistence;
using IoTDistributed.Node.Sensors;

namespace IoTDistributed.Node.Workers;

public class SensorReadingTestWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SensorReadingTestWorker> _logger;

    public SensorReadingTestWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<SensorReadingTestWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        int nodeId = _configuration.GetValue<int>("NODE_ID");

        string sensorId = nodeId switch
        {
            1 => "TEMP-001",
            2 => "PRESS-001",
            3 => "LEVEL-001",
            _ => throw new InvalidOperationException(
                $"Nó inválido: {nodeId}")
        };

        double value = nodeId switch
        {
            1 => 28.5,
            2 => 1.2,
            3 => 75.0,
            _ => 0
        };

        var reading = new SensorReading(
            EventId: Guid.NewGuid(),
            SensorId: sensorId,
            Value: value,
            CollectedAtUtc: DateTime.UtcNow,
            OriginNodeId: nodeId
        );

        await using var scope = _scopeFactory.CreateAsyncScope();

        var repository =
            scope.ServiceProvider.GetRequiredService<SensorRepository>();

        bool inserted =
            await repository.SaveAsync(reading, stoppingToken);

        _logger.LogInformation(
            "NODE {NodeId}: medição {SensorId} = {Value}. Inserida: {Inserted}",
            nodeId,
            sensorId,
            value,
            inserted);
    }
}
