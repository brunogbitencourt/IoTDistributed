
namespace IoTDistributed.Node.Sensors;

public sealed record SensorReading(
    Guid EventId,
    string SensorId,
    double Value,
    DateTime CollectedAtUtc,
    int OriginNodeId
);
