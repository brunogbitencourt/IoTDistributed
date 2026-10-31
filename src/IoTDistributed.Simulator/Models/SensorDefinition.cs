namespace IoTDistributed.Simulator.Models;

/// <summary>
/// Mesmos sensores, limites e tópicos definidos no firmware ESP32
/// (firmware/esp32-sensor-simulator/main/main.c).
/// </summary>
public sealed record SensorDefinition(
    string SensorId,
    string Topic,
    double Minimum,
    double Maximum)
{
    public static readonly IReadOnlyList<SensorDefinition> All =
    [
        new("TEMP-001", "iot/sensors/TEMP-001/readings", 20.0, 35.0),
        new("PRESS-001", "iot/sensors/PRESS-001/readings", 0.80, 1.60),
        new("LEVEL-001", "iot/sensors/LEVEL-001/readings", 15.0, 95.0),
    ];
}
