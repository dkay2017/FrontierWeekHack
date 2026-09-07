namespace Zynara.Core.Model;

/// <summary>
/// A payer + procedure denial-history summary — what the system rolls up from the
/// case record (Cosmos) to size the appeal opportunity. Aggregate, not per-case:
/// every field is a <b>visible input</b> to the Estimated Recoverable Value
/// (evaluator §14 — "keep the concept, expose the assumptions").
/// </summary>
public sealed record DenialCohort(
    string PayerPlan,
    string Procedure,
    Region Region,
    int Denied,
    int Appealed,
    int AppealsWon,
    int NotAppealed,
    decimal MeanClaimValue,
    DateOnly WindowStart,
    DateOnly WindowEnd);

/// <summary>How much weight the sample size lets the estimate carry.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum RecoveryConfidence
{
    Low,
    Medium,
    High,
}

/// <summary>
/// The appeal opportunity for one scope (or the portfolio), with every assumption
/// on the surface. <c>estimate = notAppealed × comparableWinRate × meanClaimValue</c>
/// — no invented improvement figure (evaluator §16 / guardrail §18).
/// </summary>
public sealed record RecoveryEstimate(
    string Scope,
    int Denied,
    int NotAppealed,
    int Appealed,
    int AppealsWon,
    double ComparableWinRate,
    decimal MeanClaimValue,
    decimal EstimatedRecoverableValue,
    RecoveryConfidence Confidence,
    string Formula,
    string Window)
{
    public static readonly RecoveryEstimate Empty = new(
        "—", 0, 0, 0, 0, 0d, 0m, 0m, RecoveryConfidence.Low,
        "no denial history on record", "—");
}

/// <summary>The portfolio total plus each payer/procedure scope that feeds it.</summary>
public sealed record RecoveryReport(
    RecoveryEstimate Total,
    IReadOnlyList<RecoveryEstimate> ByScope);
