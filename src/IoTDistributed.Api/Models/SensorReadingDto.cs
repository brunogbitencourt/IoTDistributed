
namespace IoTDistributed.Api.Models;

public sealed class SensorReadingDto
{
    public Guid EventId { get; set; }

    public string SensorId { get; set; } = string.Empty;

    public double Value { get; set; }

    public DateTime CollectedAtUtc { get; set; }

    public int OriginNodeId { get; set; }

    public DateTime CreatedAtUtc { get; set; }
}
