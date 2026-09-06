using Zynara.Core.Model;

namespace Zynara.Core.Gating;

/// <summary>Gate thresholds (Architecture §8 / TDD §3). Bound at startup; overridable per region later.</summary>
public sealed record GateOptions
{
    /// <summary>Minimum readiness (met / total criteria) for an auto-submit. Default 0.80.</summary>
    public double ReadinessThreshold { get; init; } = 0.80;

    /// <summary>Estimated claim value at or below which an auto-submit is allowed. Default £500.</summary>
    public decimal AutoLimit { get; init; } = 500m;
}

/// <summary>
/// The deterministic seam between reasoning and action.
/// <c>readiness ≥ threshold AND no missing criteria AND no contradictions AND value ≤ auto-limit</c>
/// → auto-submit the draft; otherwise → the human review queue. An exact-threshold
/// case passes. The verdict is always shown with its working.
/// </summary>
public sealed class Gate(GateOptions options)
{
    public Gate() : this(new GateOptions()) { }

    public GateDecision Evaluate(EvidenceGapResult gap, decimal? estimatedValue)
    {
        var reasons = new List<string>();

        var readinessOk = gap.Readiness >= options.ReadinessThreshold;
        reasons.Add($"readiness {gap.Readiness:0.00} {(readinessOk ? "≥" : "<")} {options.ReadinessThreshold:0.00}");

        var noMissing = gap.Assessment.Missing.Count == 0;
        if (!noMissing)
            reasons.Add($"{gap.Assessment.Missing.Count} criterion(a) missing");

        var noConflicts = gap.Assessment.Conflicts.Count == 0;
        if (!noConflicts)
            reasons.Add($"{gap.Assessment.Conflicts.Count} contradiction(s) in the note");

        var valueOk = estimatedValue is null || estimatedValue.Value <= options.AutoLimit;
        if (!valueOk)
            reasons.Add($"value {estimatedValue:C0} > auto-limit {options.AutoLimit:C0}");

        var autoSubmit = readinessOk && noMissing && noConflicts && valueOk;
        var verdict = autoSubmit ? "auto-submit" : "route to human review";
        return new GateDecision(autoSubmit, $"{verdict} — {string.Join("; ", reasons)}.");
    }
}
