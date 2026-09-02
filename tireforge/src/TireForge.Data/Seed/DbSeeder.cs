using Microsoft.EntityFrameworkCore;
using TireForge.Core.Model;

namespace TireForge.Data.Seed;

/// <summary>
/// Populates a fresh database (Build Plan Stage A steps 2–3):
/// 5 machines with bands + units from <c>sensor_data.json</c>, one snapshot
/// reading per machine, and ~8 past incidents in <c>History</c>.
/// Idempotent — does nothing if machines already exist.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(TireForgeDbContext db, CancellationToken ct = default)
    {
        if (await db.Machines.AnyAsync(ct))
            return;

        var file = SensorDataFile.Load();

        foreach (var m in file.Machines)
        {
            db.Machines.Add(new Machine
            {
                Id = m.MachineId,
                Name = m.Name,
                Description = m.Description,
                SeedStatus = m.Status,
                LastMaintenance = m.LastMaintenance,
                Temperature = Band(m, "temperature"),
                Pressure = Band(m, "pressure"),
                Vibration = Band(m, "vibration"),
                Rpm = Band(m, "rpm"),
            });

            db.Readings.Add(new Reading
            {
                Id = $"rdg-seed-{m.MachineId}",
                MachineId = m.MachineId,
                CapturedAt = file.Timestamp,
                Temperature = m.Readings["temperature"].Value,
                Pressure = m.Readings["pressure"].Value,
                Vibration = m.Readings["vibration"].Value,
                Rpm = m.Readings["rpm"].Value,
                IsAnomaly = null,
                Mode = null,
            });
        }

        db.History.AddRange(SeedHistory());

        await db.SaveChangesAsync(ct);
    }

    private static SensorBand Band(SensorMachine m, string sensor) => new()
    {
        Min = m.Thresholds[sensor].Min,
        Max = m.Thresholds[sensor].Max,
        Unit = m.Readings.TryGetValue(sensor, out var r) ? r.Unit : "",
    };

    /// <summary>~8 documented past incidents used by HistoryMatch / T2 (Stage E).</summary>
    private static IEnumerable<HistoryIncident> SeedHistory() => new[]
    {
        new HistoryIncident
        {
            Id = "inc-001", MachineId = "MX-001", OccurredOn = new(2025, 11, 8),
            Signature = "temperature-high", Fault = "drive motor overheating", Severity = Severity.Warn,
            Resolution = "cleaned cooling fins, replaced thermostat, verified airflow",
        },
        new HistoryIncident
        {
            Id = "inc-002", MachineId = "MX-001", OccurredOn = new(2026, 1, 22),
            Signature = "vibration-high", Fault = "mixing blade imbalance", Severity = Severity.Info,
            Resolution = "rebalanced mixing blades, torqued mounting bolts to spec",
        },
        new HistoryIncident
        {
            Id = "inc-003", MachineId = "EX-002", OccurredOn = new(2025, 12, 3),
            Signature = "pressure-high+temperature-high", Fault = "die head blockage / restricted flow", Severity = Severity.Crit,
            Resolution = "cleared die head, replaced screen pack, flushed barrel",
        },
        new HistoryIncident
        {
            Id = "inc-004", MachineId = "EX-002", OccurredOn = new(2026, 2, 17),
            Signature = "vibration-high", Fault = "screw thrust bearing wear", Severity = Severity.Warn,
            Resolution = "replaced screw thrust bearing, re-aligned gearbox coupling",
        },
        new HistoryIncident
        {
            Id = "inc-005", MachineId = "CP-003", OccurredOn = new(2025, 10, 29),
            Signature = "temperature-high+vibration-high", Fault = "platen bearing failure", Severity = Severity.Crit,
            Resolution = "replaced platen bearings, re-greased, re-calibrated press alignment",
        },
        new HistoryIncident
        {
            Id = "inc-006", MachineId = "CP-003", OccurredOn = new(2026, 3, 11),
            Signature = "pressure-high", Fault = "hydraulic relief valve stuck closed", Severity = Severity.Warn,
            Resolution = "rebuilt hydraulic relief valve, replaced pressure transducer",
        },
        new HistoryIncident
        {
            Id = "inc-007", MachineId = "CU-004", OccurredOn = new(2026, 1, 5),
            Signature = "temperature-high", Fault = "coolant pump underperforming", Severity = Severity.Info,
            Resolution = "replaced coolant pump impeller, topped up refrigerant charge",
        },
        new HistoryIncident
        {
            Id = "inc-008", MachineId = "IS-005", OccurredOn = new(2026, 4, 2),
            Signature = "vibration-high", Fault = "spindle bearing wear", Severity = Severity.Warn,
            Resolution = "replaced spindle bearing assembly, re-zeroed dimensional gauges",
        },
    };
}
