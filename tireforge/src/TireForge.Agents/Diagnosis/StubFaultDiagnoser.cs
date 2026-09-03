using System.Text;
using TireForge.Core.Agents;
using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents;

/// <summary>
/// Deterministic A2 stub (Build Plan Stage F step 2). Names a probable fault from
/// the breach pattern + history, and derives a severity and a confidence tuned so
/// the Gate sees a realistic spread (some &lt; 0.70, some Crit). Real Foundry agent
/// swaps in at Stage M.
/// </summary>
public sealed class StubFaultDiagnoser : IFaultDiagnoser
{
    public Task<FaultVerdict> DiagnoseAsync(
        Reading reading, ThresholdReport t1, HistoryReport t2, AnomalyVerdict a1, CancellationToken ct = default)
    {
        if (!a1.IsAnomaly)
            throw new InvalidOperationException("Fault Diagnosis runs only on anomalous readings.");

        var breaches = t1.Breaches;
        var fault = NameFault(breaches, t2);
        var severity = FaultHeuristics.Escalate(t1.Severity, t2);
        var confidence = FaultHeuristics.Score(t1, t2);

        var cites = new List<string> { reading.Id };
        cites.AddRange(t2.Cites);

        var text = BuildText(reading, t1, t2, fault, severity, confidence);

        return Task.FromResult(new FaultVerdict
        {
            Fault = fault,
            Severity = severity,
            Confidence = confidence,
            Text = text,
            Cites = cites,
        }.Validate());
    }

    // --- Fault naming ---------------------------------------------------------
    // Exact prior incident wins (grounded). Otherwise the Challenge 1 rubric.
    private static string NameFault(IReadOnlyList<SensorEvaluation> breaches, HistoryReport t2)
    {
        if (t2.Exact && t2.Incidents.Count > 0)
            return t2.Incidents[0].Fault;

        var high = breaches.Where(b => b.Status == SensorStatus.High).Select(b => b.Sensor).ToHashSet();
        bool T = high.Contains(SensorKind.Temperature);
        bool P = high.Contains(SensorKind.Pressure);
        bool V = high.Contains(SensorKind.Vibration);

        if (breaches.Count >= 3)
            return "compound failure — multiple systems out of spec";
        if (T && P)
            return "blockage or restricted flow";
        if (T && V)
            return "bearing failure or lubrication breakdown";
        if (V)
            return "bearing wear, misalignment, or imbalance";
        if (T)
            return "cooling / thermal regulation fault";
        if (P)
            return "pressure regulation fault";

        return breaches.Count > 0
            ? $"{breaches[0].Sensor.Slug()} out of spec — cause undetermined"
            : "no fault indicated";
    }

    private static string BuildText(
        Reading reading, ThresholdReport t1, HistoryReport t2, string fault, Severity severity, double confidence)
    {
        var sb = new StringBuilder($"A2 {reading.Id} {reading.MachineId}: {fault} — ");
        sb.Append($"severity {severity}, confidence {confidence:0.00}. ");
        sb.Append(t1.Breaches.Count > 0
            ? $"Breaches: {string.Join(", ", t1.Breaches.Select(b => b.Sensor.Slug()))}. "
            : "No threshold breach. ");
        sb.Append(t2.AnyMatch
            ? $"{(t2.Exact ? "Prior" : "Closest prior")}: {string.Join("; ", t2.Incidents.Select(i => $"{i.Id} {i.Fault}"))}."
            : "No comparable history.");
        return sb.ToString();
    }
}
