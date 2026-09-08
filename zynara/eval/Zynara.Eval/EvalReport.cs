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

    // agents vs. one credible generalist (TDD §3.1)
    BaselineReport Baseline,

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
        sb.AppendLine($"  agents vs. one generalist  (baseline: {Baseline.Source})");
        sb.AppendLine($"    route agreement    agents {RouteAgreement,6:P1}   generalist {Baseline.RouteAgreement,6:P1}");
        sb.AppendLine($"    unsafe automation  agents {UnsafeAutomations,6}   generalist {Baseline.UnsafeAutomations,6}");
        if (Baseline.Source == BaselineReport.LlmSource)
        {
            sb.AppendLine($"    mandatory FN       agents {MandatoryFalseNegatives,6}   generalist {Baseline.MandatoryFalseNegatives,6}");
            sb.AppendLine($"    evidence recall    agents {EvidenceRecall,6:P1}   generalist {Baseline.EvidenceRecall,6:P1}");
            sb.AppendLine($"    hallucinations     agents {HallucinatedReferences,6}   generalist {Baseline.HallucinatedReferences,6}");
            sb.AppendLine($"    safe abstention    agents {AbstainTaken,6}   generalist {Baseline.AbstainTaken,6}  (of {AbstainExpected})");
            sb.AppendLine($"    contradictions     agents {"n/a",6}   generalist {Baseline.ContradictionsCaught,6}  (of {Baseline.ContradictionsExpected})");
        }
        else
        {
            sb.AppendLine($"    (deterministic keyword baseline — run `dotnet run --project tools/Zynara.BaselineRefresh`");
            sb.AppendLine($"     to capture the credible-generalist fixtures and score the full comparison)");
        }
        if (CaseLines.Count > 0)
        {
            sb.AppendLine(new string('-', 52));
            foreach (var line in CaseLines) sb.AppendLine($"  {line}");
        }
        return sb.ToString();
    }
}

/// <summary>
/// The single-generalist comparison (TDD §3.1). <see cref="Source"/> is
/// <c>llm-fixture</c> once <c>baseline-fixtures/</c> is captured for every case;
/// until then it is the deterministic keyword straw man and only route agreement
/// + unsafe automation are meaningful.
/// </summary>
public sealed record BaselineReport(
    string Source,
    double RouteAgreement,
    int UnsafeAutomations,
    int MandatoryFalseNegatives,
    int MandatoryUnmetTotal,
    double EvidencePrecision,
    double EvidenceRecall,
    double PrecedentCitationAccuracy,
    int HallucinatedReferences,
    double PolicyCitationAccuracy,
    int AbstainExpected,
    int AbstainTaken,
    int ContradictionsExpected,
    int ContradictionsCaught)
{
    public const string LlmSource = "llm-fixture";
    public const string KeywordSource = "deterministic-keyword";
}
