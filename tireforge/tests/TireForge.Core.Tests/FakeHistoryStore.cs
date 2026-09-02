using TireForge.Core.Abstractions;
using TireForge.Core.Model;

namespace TireForge.Core.Tests;

/// <summary>In-memory <see cref="IHistoryStore"/> seeded with the same ~8 incidents as the DB seeder.</summary>
public sealed class FakeHistoryStore : IHistoryStore
{
    private readonly List<HistoryIncident> _incidents;

    public FakeHistoryStore(IEnumerable<HistoryIncident>? incidents = null)
        => _incidents = (incidents ?? Default()).ToList();

    public Task<IReadOnlyList<HistoryIncident>> MatchAsync(string machineId, string signature, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HistoryIncident>>(
            _incidents.Where(h => h.MachineId == machineId && h.Signature == signature)
                .OrderByDescending(h => h.OccurredOn).ToList());

    public Task<IReadOnlyList<HistoryIncident>> ForMachineAsync(string machineId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<HistoryIncident>>(
            _incidents.Where(h => h.MachineId == machineId)
                .OrderByDescending(h => h.OccurredOn).ToList());

    public static IEnumerable<HistoryIncident> Default() => new[]
    {
        Inc("inc-001", "MX-001", 2025, 11, 8, "temperature-high", "drive motor overheating", Severity.Warn),
        Inc("inc-002", "MX-001", 2026, 1, 22, "vibration-high", "mixing blade imbalance", Severity.Info),
        Inc("inc-003", "EX-002", 2025, 12, 3, "pressure-high+temperature-high", "die head blockage / restricted flow", Severity.Crit),
        Inc("inc-004", "EX-002", 2026, 2, 17, "vibration-high", "screw thrust bearing wear", Severity.Warn),
        Inc("inc-005", "CP-003", 2025, 10, 29, "temperature-high+vibration-high", "platen bearing failure", Severity.Crit),
        Inc("inc-006", "CP-003", 2026, 3, 11, "pressure-high", "hydraulic relief valve stuck closed", Severity.Warn),
        Inc("inc-007", "CU-004", 2026, 1, 5, "temperature-high", "coolant pump underperforming", Severity.Info),
        Inc("inc-008", "IS-005", 2026, 4, 2, "vibration-high", "spindle bearing wear", Severity.Warn),
    };

    private static HistoryIncident Inc(
        string id, string machineId, int y, int m, int d, string sig, string fault, Severity sev) => new()
    {
        Id = id, MachineId = machineId, OccurredOn = new DateOnly(y, m, d),
        Signature = sig, Fault = fault, Severity = sev, Resolution = "…",
    };
}
