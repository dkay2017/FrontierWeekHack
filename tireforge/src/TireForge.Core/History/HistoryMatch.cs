using System.Text;
using TireForge.Core.Abstractions;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.History;

/// <summary>Result of HistoryMatch / T2 for one reading (Build Plan Stage E).</summary>
public sealed record HistoryReport(
    string ReadingId,
    string MachineId,
    string Signature,
    IReadOnlyList<HistoryIncident> Incidents,
    bool Exact,
    string Trace)
{
    /// <summary>Incident ids the diagnosis may cite (<c>inc-…</c>).</summary>
    public IReadOnlyList<string> Cites => Incidents.Select(i => i.Id).ToList();

    public bool AnyMatch => Incidents.Count > 0;
}

/// <summary>
/// Pure history correlation (Build Plan Stage E). Given the T1 report, derives a
/// fault signature and looks for comparable past incidents on the same machine:
/// an exact signature match first, then the best token-overlap fallback. Emits a
/// <c>T2 …</c> trace line citing the matched <c>inc-</c> ids (step 3).
/// </summary>
public static class HistoryMatch
{
    public static async Task<HistoryReport> RunAsync(
        Machine machine, ThresholdReport t1, IHistoryStore store, CancellationToken ct = default)
    {
        if (t1.MachineId != machine.Id)
            throw new ArgumentException($"T1 report is for '{t1.MachineId}', not '{machine.Id}'.");

        var signature = FaultSignature.From(t1);

        if (signature.Length == 0)
        {
            return new HistoryReport(t1.ReadingId, machine.Id, signature, Array.Empty<HistoryIncident>(),
                Exact: false, Trace: Trace(t1.ReadingId, machine.Id, signature, Array.Empty<HistoryIncident>(), false));
        }

        var exact = await store.MatchAsync(machine.Id, signature, ct);
        if (exact.Count > 0)
            return new HistoryReport(t1.ReadingId, machine.Id, signature, exact, true,
                Trace(t1.ReadingId, machine.Id, signature, exact, true));

        var overlapping = (await store.ForMachineAsync(machine.Id, ct))
            .Select(inc => (inc, overlap: FaultSignature.Overlap(signature, inc.Signature)))
            .Where(x => x.overlap > 0)
            .OrderByDescending(x => x.overlap)
            .ThenByDescending(x => x.inc.OccurredOn)
            .Select(x => x.inc)
            .ToList();

        return new HistoryReport(t1.ReadingId, machine.Id, signature, overlapping, false,
            Trace(t1.ReadingId, machine.Id, signature, overlapping, false));
    }

    private static string Trace(
        string readingId, string machineId, string signature,
        IReadOnlyList<HistoryIncident> incidents, bool exact)
    {
        var sb = new StringBuilder($"T2 {readingId} {machineId}: signature '{signature}' — ");

        if (incidents.Count == 0)
        {
            sb.Append("no prior incidents");
            return sb.ToString();
        }

        sb.Append(exact ? "exact match, " : "closest match, ");
        sb.Append(incidents.Count == 1 ? "1 prior incident " : $"{incidents.Count} prior incidents ");
        sb.Append('[');
        sb.Append(string.Join("; ", incidents.Select(i => $"{i.Id}: {i.Fault} ({i.OccurredOn:yyyy-MM-dd})")));
        sb.Append(']');
        return sb.ToString();
    }
}
