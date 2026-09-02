using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TireForge.Data.Seed;

/// <summary>
/// Deserialisation shape for <c>Seed/sensor_data.json</c> — a verbatim copy of
/// <c>factory/challenge-1-build/sensor_data.json</c>, embedded as a resource.
/// </summary>
public sealed class SensorDataFile
{
    [JsonPropertyName("factory")] public string Factory { get; set; } = "";
    [JsonPropertyName("timestamp")] public DateTimeOffset Timestamp { get; set; }
    [JsonPropertyName("machines")] public List<SensorMachine> Machines { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Load the embedded sample data.</summary>
    public static SensorDataFile Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var name = asm.GetManifestResourceNames()
            .Single(n => n.EndsWith("sensor_data.json", StringComparison.Ordinal));
        using var stream = asm.GetManifestResourceStream(name)!;
        return JsonSerializer.Deserialize<SensorDataFile>(stream, Options)
            ?? throw new InvalidOperationException("sensor_data.json failed to deserialise.");
    }
}

public sealed class SensorMachine
{
    [JsonPropertyName("machine_id")] public string MachineId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "normal";
    [JsonPropertyName("last_maintenance")] public DateOnly? LastMaintenance { get; set; }
    [JsonPropertyName("readings")] public Dictionary<string, SensorReading> Readings { get; set; } = new();
    [JsonPropertyName("thresholds")] public Dictionary<string, SensorThreshold> Thresholds { get; set; } = new();
}

public sealed class SensorReading
{
    [JsonPropertyName("value")] public double Value { get; set; }
    [JsonPropertyName("unit")] public string Unit { get; set; } = "";
}

public sealed class SensorThreshold
{
    [JsonPropertyName("min")] public double Min { get; set; }
    [JsonPropertyName("max")] public double Max { get; set; }
}
