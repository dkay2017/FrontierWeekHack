using TireForge.Core.Model;

namespace TireForge.Core.Gating;

/// <summary>The Gate's verdict (Build Plan Stage G).</summary>
public sealed record GateDecision(GateRoute Route, string Reason);

/// <summary>
/// Confidence-gated oversight (invariant 1.3):
/// <c>confidence &lt; 0.70</c> OR <c>severity == Crit</c> → human review;
/// exactly <c>0.70</c> and not critical → auto-issue.
/// </summary>
public static class Gate
{
    /// <summary>Minimum confidence for the auto route. Values at this exact number pass.</summary>
    public const double AutoConfidence = 0.70;

    public static GateDecision Evaluate(Severity severity, double confidence)
    {
        var lowConfidence = confidence < AutoConfidence;
        var critical = severity == Severity.Crit;

        if (!lowConfidence && !critical)
            return new GateDecision(GateRoute.Auto,
                $"confidence {confidence:0.00} ≥ {AutoConfidence:0.00}, severity {severity} — auto-issue");

        var reasons = new List<string>();
        if (critical) reasons.Add($"severity {severity}");
        if (lowConfidence) reasons.Add($"confidence {confidence:0.00} < {AutoConfidence:0.00}");
        return new GateDecision(GateRoute.Review, string.Join("; ", reasons) + " — human review");
    }

    /// <summary>Evaluate and record the outcome on the diagnosis row (Stage G step 2).</summary>
    public static GateDecision Apply(Diagnosis diagnosis)
    {
        var decision = Evaluate(diagnosis.Severity, diagnosis.Confidence);
        diagnosis.Route = decision.Route;
        diagnosis.GateReason = decision.Reason;
        return decision;
    }
}
