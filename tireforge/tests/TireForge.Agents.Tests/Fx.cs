using TireForge.Core.Agents;
using TireForge.Core.History;
using TireForge.Core.Model;

namespace TireForge.Agents.Tests;

/// <summary>Small fixtures for the agent-stub tests (no data layer).</summary>
internal static class Fx
{
    public static Machine Mixer() => new()
    {
        Id = "MX-001", Name = "mixer",
        Temperature = B(60, 90, "celsius"), Pressure = B(2.0, 4.0, "bar"),
        Vibration = B(0, 4.5, "mm/s"), Rpm = B(40, 65, "rpm"),
    };

    public static Machine CuringPress() => new()
    {
        Id = "CP-003", Name = "curing_press",
        Temperature = B(140, 180, "celsius"), Pressure = B(12.0, 16.0, "bar"),
        Vibration = B(0, 3.0, "mm/s"), Rpm = B(0, 0, "rpm"),
    };

    public static Machine InspectionStation() => new()
    {
        Id = "IS-005", Name = "inspection_station",
        Temperature = B(18, 30, "celsius"), Pressure = B(0.8, 1.2, "bar"),
        Vibration = B(0, 4.0, "mm/s"), Rpm = B(1500, 2200, "rpm"),
    };

    public static Reading Reading(string machineId, double t, double p, double v, double r) => new()
    {
        Id = Ids.Reading(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)),
        MachineId = machineId,
        CapturedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };

    public static HistoryReport History(
        string readingId, string machineId, string signature, bool exact, params HistoryIncident[] incidents) =>
        new(readingId, machineId, signature, incidents, exact, $"T2 {readingId} {machineId}: signature '{signature}'");

    public static HistoryReport NoHistory(string readingId, string machineId, string signature) =>
        new(readingId, machineId, signature, Array.Empty<HistoryIncident>(), false,
            $"T2 {readingId} {machineId}: signature '{signature}' — no prior incidents");

    public static HistoryIncident Incident(string id, string machineId, string sig, string fault, Severity sev) => new()
    {
        Id = id, MachineId = machineId, OccurredOn = new DateOnly(2026, 1, 1),
        Signature = sig, Fault = fault, Severity = sev, Resolution = "…",
    };

    private static SensorBand B(double min, double max, string unit) => new() { Min = min, Max = max, Unit = unit };
}
