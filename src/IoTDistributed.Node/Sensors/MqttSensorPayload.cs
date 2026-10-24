
using System.Text.Json.Serialization;

namespace IoTDistributed.Node.Sensors;

public sealed class MqttSensorPayload
{
    [JsonPropertyName("event_id")]
    public Guid? EventId { get; init; }

    [JsonPropertyName("sensor_id")]
    public string? SensorId { get; init; }

    [JsonPropertyName("value")]
    public double? Value { get; init; }

    [JsonPropertyName("collected_at_utc")]
    public DateTimeOffset? CollectedAtUtc { get; init; }
}
