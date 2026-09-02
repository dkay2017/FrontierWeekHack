namespace TireForge.Core.Model;

/// <summary>
/// A production machine on the TireForge floor. Seeded from
/// <c>factory/challenge-1-build/sensor_data.json</c> (5 machines).
/// </summary>
public class Machine
{
    /// <summary>Machine ID, e.g. <c>MX-001</c>.</summary>
    public required string Id { get; set; }

    /// <summary>Short name, e.g. <c>mixer</c>.</summary>
    public required string Name { get; set; }

    public string Description { get; set; } = "";

    /// <summary>Seed status label (<c>normal</c> / <c>warning</c> / <c>critical</c>) — the
    /// snapshot state from the sample data, not authoritative once readings flow.</summary>
    public string SeedStatus { get; set; } = "normal";

    public DateOnly? LastMaintenance { get; set; }

    /// <summary>Acceptable operating band per sensor.</summary>
    public SensorBand Temperature { get; set; } = new();
    public SensorBand Pressure { get; set; } = new();
    public SensorBand Vibration { get; set; } = new();
    public SensorBand Rpm { get; set; } = new();

    /// <summary>Readings recorded against this machine.</summary>
    public List<Reading> Readings { get; } = new();

    /// <summary>Band for a given sensor kind.</summary>
    public SensorBand Band(SensorKind kind) => kind switch
    {
        SensorKind.Temperature => Temperature,
        SensorKind.Pressure => Pressure,
        SensorKind.Vibration => Vibration,
        SensorKind.Rpm => Rpm,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>An inclusive [Min, Max] acceptable range for one sensor, with its unit.</summary>
public class SensorBand
{
    public double Min { get; set; }
    public double Max { get; set; }
    public string Unit { get; set; } = "";

    public bool InSpec(double value) => value >= Min && value <= Max;
}
