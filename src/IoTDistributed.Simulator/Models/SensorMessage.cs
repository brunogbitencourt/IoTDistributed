using System.Text.Json.Serialization;

namespace IoTDistributed.Simulator.Models;

/// <summary>
/// Formato JSON idêntico ao enviado pelo firmware ESP32 e recebido pelo
/// MqttSensorPayload dos nós .NET.
/// </summary>
public sealed record SensorMessage(
    [property: JsonPropertyName("event_id")] Guid EventId,
    [property: JsonPropertyName("sensor_id")] string SensorId,
    [property: JsonPropertyName("value")] double Value,
    [property: JsonPropertyName("collected_at_utc")] string CollectedAtUtc);
