
namespace IoTDistributed.Api.Models;

public sealed class SensorDto
{
    public string SensorId { get; set; } = string.Empty;

    public string SensorType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int OwnerNodeId { get; set; }

    public string? MqttTopic { get; set; }
}
