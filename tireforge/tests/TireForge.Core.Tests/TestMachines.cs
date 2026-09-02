using TireForge.Core.Model;

namespace TireForge.Core.Tests;

/// <summary>
/// The 5 machines from <c>factory/challenge-1-build/sensor_data.json</c>, built
/// in-memory so Core tests need no data layer.
/// </summary>
public static class TestMachines
{
    public static Machine Mixer() => new()
    {
        Id = "MX-001", Name = "mixer", Description = "Blends raw rubber compounds",
        SeedStatus = "warning",
        Temperature = B(60, 90, "celsius"),
        Pressure = B(2.0, 4.0, "bar"),
        Vibration = B(0, 4.5, "mm/s"),
        Rpm = B(40, 65, "rpm"),
    };

    public static Machine Extruder() => new()
    {
        Id = "EX-002", Name = "extruder", Description = "Shapes rubber into tread",
        SeedStatus = "normal",
        Temperature = B(100, 130, "celsius"),
        Pressure = B(10.0, 15.0, "bar"),
        Vibration = B(0, 3.5, "mm/s"),
        Rpm = B(20, 40, "rpm"),
    };

    public static Machine CuringPress() => new()
    {
        Id = "CP-003", Name = "curing_press", Description = "Vulcanizes tire",
        SeedStatus = "critical",
        Temperature = B(140, 180, "celsius"),
        Pressure = B(12.0, 16.0, "bar"),
        Vibration = B(0, 3.0, "mm/s"),
        Rpm = B(0, 0, "rpm"),
    };

    public static Machine CoolingUnit() => new()
    {
        Id = "CU-004", Name = "cooling_unit", Description = "Cools cured tires",
        SeedStatus = "normal",
        Temperature = B(20, 45, "celsius"),
        Pressure = B(0.8, 1.5, "bar"),
        Vibration = B(0, 2.0, "mm/s"),
        Rpm = B(80, 150, "rpm"),
    };

    public static Machine InspectionStation() => new()
    {
        Id = "IS-005", Name = "inspection_station", Description = "QA analysis",
        SeedStatus = "warning",
        Temperature = B(18, 30, "celsius"),
        Pressure = B(0.8, 1.2, "bar"),
        Vibration = B(0, 4.0, "mm/s"),
        Rpm = B(1500, 2200, "rpm"),
    };

    public static IEnumerable<Machine> All()
    {
        yield return Mixer();
        yield return Extruder();
        yield return CuringPress();
        yield return CoolingUnit();
        yield return InspectionStation();
    }

    /// <summary>The snapshot readings from <c>sensor_data.json</c> (t, p, v, r).</summary>
    public static Reading Snapshot(string machineId) => machineId switch
    {
        "MX-001" => R(machineId, 92.3, 3.1, 4.8, 58),
        "EX-002" => R(machineId, 115.0, 12.5, 2.1, 30),
        "CP-003" => R(machineId, 198.5, 18.2, 7.3, 0),
        "CU-004" => R(machineId, 35.2, 1.0, 0.8, 120),
        "IS-005" => R(machineId, 28.0, 1.0, 5.2, 1800),
        _ => throw new ArgumentOutOfRangeException(nameof(machineId)),
    };

    private static SensorBand B(double min, double max, string unit) => new() { Min = min, Max = max, Unit = unit };

    private static Reading R(string machineId, double t, double p, double v, double r) => new()
    {
        Id = $"rdg-seed-{machineId}",
        MachineId = machineId,
        CapturedAt = new DateTimeOffset(2026, 5, 13, 9, 30, 0, TimeSpan.Zero),
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };
}
