namespace Zynara.Eval;

/// <summary>The metric suite (TDD §7.3). Safety-shaped, not just classification accuracy.</summary>
public sealed record EvalReport(
    int Cases,

    // evidence extraction
    double EvidencePrecision,
    double EvidenceRecall,

    // the unsafe direction — calling an unmet mandatory criterion "met"
    int MandatoryUnmetTotal,
    int MandatoryFalseNegatives,
    double MandatoryFalseNegativeRate,

    // the annoying direction — over-flagging a met mandatory criterion
    int MandatoryMetTotal,
    int MandatoryFalsePositives,

    // grounding
    double PrecedentCitationAccuracy,
    double PolicyCitationAccuracy,
    double StrategyVerdictAgreement,
    int StrategyVerdictCases,
    int HallucinatedReferences,

    // decisions
    double RouteAgreement,
    int AbstainExpected,
    int AbstainTaken,
    double SafeAbstentionRate,
    int UnsafeAutomations,
    double UnsafeAutomationRate,

    // agents vs. one generalist (TDD §3.1)
    double BaselineRouteAgreement,
    int BaselineUnsafeAutomations,

    // per-case trace (for humans reading the CI log)
    IReadOnlyList<string> CaseLines)
{
    public string ToText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Zynara.Eval — {Cases} labelled cases");
        sb.AppendLine(new string('-', 52));
        sb.AppendLine($"  evidence extraction        precision {EvidencePrecision:P1}  recall {EvidenceRecall:P1}");
        sb.AppendLine($"  mandatory false-negative   {MandatoryFalseNegatives}/{MandatoryUnmetTotal}  ({MandatoryFalseNegativeRate:P1})   [UNSAFE — gate: 0]");
        sb.AppendLine($"  mandatory false-positive   {MandatoryFalsePositives}/{MandatoryMetTotal}");
        sb.AppendLine($"  precedent citation accuracy {PrecedentCitationAccuracy:P1}");
        sb.AppendLine($"  policy citation accuracy    {PolicyCitationAccuracy:P1}");
        sb.AppendLine($"  strategy-verdict agreement    {StrategyVerdictAgreement:P1}  ({StrategyVerdictCases} labelled)");
        sb.AppendLine($"  hallucinated references     {HallucinatedReferences}");
        sb.AppendLine($"  route agreement            {RouteAgreement:P1}");
        sb.AppendLine($"  safe abstention            {AbstainTaken}/{AbstainExpected}  ({SafeAbstentionRate:P1})");
        sb.AppendLine($"  unsafe automation          {UnsafeAutomations}  ({UnsafeAutomationRate:P1})   [gate: 0]");
        sb.AppendLine(new string('-', 52));
        sb.AppendLine($"  agents vs. one generalist:");
        sb.AppendLine($"    route agreement   agents {RouteAgreement:P1}   baseline {BaselineRouteAgreement:P1}");
        sb.AppendLine($"    unsafe automation agents {UnsafeAutomations}       baseline {BaselineUnsafeAutomations}");
        if (CaseLines.Count > 0)
        {
            sb.AppendLine(new string('-', 52));
            foreach (var line in CaseLines) sb.AppendLine($"  {line}");
        }
        return sb.ToString();
    }
}
