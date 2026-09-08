using Zynara.Core.Agents;

namespace Zynara.Core.Model;

// Typed outputs of the deterministic spokes and the Gate. Every value here is
// owned by deterministic code (hybrid principle) — the agent text is carried
// alongside, never as the decision.

/// <summary>NeedsAuthCheck result — the yes/no + code is deterministic; <see cref="Note"/> is agent prose.</summary>
public sealed record NeedsAuthResult(
    bool AuthRequired,
    string? RequiredCode,
    string? PolicyRef,
    bool Ambiguous,
    string Note);

/// <summary>
/// EvidenceGapMatch result. The agent's per-criterion findings, plus the evidence
/// dimensions of the decision model derived here — mandatory criteria are tracked
/// separately and never averaged (evaluator finding #3).
/// </summary>
public sealed record EvidenceGapResult(
    EvidenceGapAssessment Assessment,
    bool MandatoryPass,
    int MandatoryTotal,
    int SupportingDocumented,
    int SupportingPartial,
    int SupportingMissing,
    bool ContradictionDetected)
{
    public EvidenceQuality Quality => Assessment.Quality;

    /// <summary>Ids of every mandatory criterion that is not Documented — the reason a case is not ready to submit.</summary>
    public IReadOnlyList<string> UnmetMandatory { get; init; } = Array.Empty<string>();
}

/// <summary>How strongly the precedent corpus supports the case.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum PrecedentSupport
{
    Strong,
    Moderate,
    Weak,
    None,
}

/// <summary>AppealMatch result — the ranked shortlist, the agent recommendation, and the derived support level.</summary>
public sealed record AppealMatchResult(
    IReadOnlyList<PrecedentMatch> Shortlist,
    StrategyRecommendation Recommendation,
    PrecedentSupport Support);

/// <summary>An advisory flag raised by ExpiryMath or PolicyDiff — no Gate, no outbound action.</summary>
public sealed record EarlyWarning(
    string Id,
    string Kind,          // "expiry" | "drift"
    string SubjectRef,    // authId or policyRef
    string Text,
    DateOnly RaisedOn);

/// <summary>ContradictionCheck result — claims that conflict <b>within</b> the clinical note (evaluator #4).</summary>
public sealed record ContradictionResult(
    int ClaimsExtracted,
    IReadOnlyList<ClaimConflict> Conflicts)
{
    public bool HasConflicts => Conflicts.Count > 0;
    public static readonly ContradictionResult None = new(0, Array.Empty<ClaimConflict>());
}

/// <summary>ExpiryMath result.</summary>
public sealed record ExpiryResult(int DaysOfMargin, EarlyWarning? Warning);

/// <summary>PolicyDiff result.</summary>
public sealed record DriftResult(
    IReadOnlyList<string> AddedCriteria,
    IReadOnlyList<string> RemovedCriteria,
    EarlyWarning? Warning);

/// <summary>The four routes the Gate can take (evaluator finding #3).</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum GateRoute
{
    /// <summary>Ready — the draft is gap-checked and Critic-cleared; a reviewer approves the send.</summary>
    ReadyToSubmit,

    /// <summary>Fixable gaps — return to the clinician before submitting.</summary>
    Strengthen,

    /// <summary>A mandatory criterion is unmet, a contradiction, or over the value limit — a reviewer decides.</summary>
    HumanReview,

    /// <summary>Not enough reliable evidence to make a recommendation — the system declines to advise.</summary>
    Abstain,
}

/// <summary>The evidence + precedent dimensions the Gate weighed — always shown with the verdict.</summary>
public sealed record DecisionModel(
    bool MandatoryPass,
    int MandatoryTotal,
    int SupportingDocumented,
    int SupportingPartial,
    int SupportingMissing,
    EvidenceQuality EvidenceQuality,
    bool ContradictionDetected,
    PrecedentSupport PrecedentSupport,
    IReadOnlyList<string> UnmetMandatory);

/// <summary>The Gate's verdict — deterministic, always carrying its working.</summary>
public sealed record GateDecision(
    GateRoute Route,
    DecisionModel Model,
    string Reason)
{
    public bool ReadyToSubmit => Route == GateRoute.ReadyToSubmit;
}

/// <summary>The assembled prior-authorisation submission a reviewer sees.</summary>
public sealed record SubmissionDraft(
    string RequestId,
    string PayerPlan,
    string Procedure,
    string? RequiredCode,
    string Body,
    IReadOnlyList<string> CitedCriteria,
    IReadOnlyList<string> CitedPrecedents);

/// <summary>Everything one run of the pipeline produced.</summary>
public sealed record PipelineResult(
    string RequestId,
    NeedsAuthResult NeedsAuth,
    EvidenceGapResult? Gap,
    AppealMatchResult? Appeal,
    CriticReview? Critic,
    GateDecision? Gate,
    SubmissionDraft? Draft)
{
    /// <summary>True when needs-auth resolved to "not required" — nothing further ran.</summary>
    public bool StoppedEarly => !NeedsAuth.AuthRequired;

    /// <summary>What this run actually did — the measured half of the before/after benchmark (P2-2).</summary>
    public PipelineMetrics Metrics { get; init; } = PipelineMetrics.Empty;

    /// <summary>Claims that conflict within the clinical note (P2-3). Empty when there is nothing to flag.</summary>
    public ContradictionResult Contradiction { get; init; } = ContradictionResult.None;
}

/// <summary>Measured facts about one pipeline run. Every field is counted, not estimated (evaluator §16).</summary>
public sealed record PipelineMetrics(
    int CriteriaChecked,
    int EvidenceGapsFound,
    int PrecedentsConsidered,
    int ReasoningStepsRun,
    long AssembledInMs)
{
    public static readonly PipelineMetrics Empty = new(0, 0, 0, 0, 0);
}
