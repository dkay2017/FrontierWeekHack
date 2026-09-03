using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Agents;

/// <summary>
/// The deterministic parts of a fault verdict — severity escalation and a
/// confidence score derived from T1 + T2. Shared by <c>StubFaultDiagnoser</c> and
/// the Stage-M Foundry diagnoser (Decision D12: the agent writes the prose, this
/// owns the numbers that drive the Gate).
/// </summary>
public static class FaultHeuristics
{
    /// <summary>T1 severity, raised to a matched prior incident's severity if that was worse.</summary>
    public static Severity Escalate(Severity t1Severity, HistoryReport t2)
    {
        if (t2.Exact && t2.Incidents.Count > 0 && t2.Incidents[0].Severity > t1Severity)
            return t2.Incidents[0].Severity;
        return t1Severity;
    }

    /// <summary>
    /// Confidence 0–1: 0.50 base, +0.25 exact prior / +0.10 near prior,
    /// +0.10 one clean signal / −0.15 compound, +0.10 far out of band / −0.10 marginal.
    /// </summary>
    public static double Score(ThresholdReport t1, HistoryReport t2)
    {
        var score = 0.50;

        if (t2.Exact) score += 0.25;
        else if (t2.AnyMatch) score += 0.10;

        var breachCount = t1.Breaches.Count;
        if (breachCount == 1) score += 0.10;
        else if (breachCount >= 3) score -= 0.15;

        var worst = t1.Breaches.Count > 0 ? t1.Breaches.Max(b => b.DeviationPct) : 0;
        if (worst >= 40) score += 0.10;
        else if (worst < 8) score -= 0.10;

        return Math.Round(Math.Clamp(score, 0.05, 0.98), 2);
    }
}
