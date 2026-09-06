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

/// <summary>EvidenceGapMatch result — the assessment is the agent's reading; <see cref="Readiness"/> is derived here.</summary>
public sealed record EvidenceGapResult(
    EvidenceGapAssessment Assessment,
    double Readiness)
{
    public bool HasGaps => Assessment.Missing.Count > 0 || Assessment.Conflicts.Count > 0;
}

/// <summary>AppealMatch result — the ranked precedent shortlist plus the agent's recommendation.</summary>
public sealed record AppealMatchResult(
    IReadOnlyList<Precedent> Shortlist,
    AppealRecommendation Recommendation);

/// <summary>An advisory flag raised by ExpiryMath or PolicyDiff — no Gate, no outbound action.</summary>
public sealed record EarlyWarning(
    string Id,
    string Kind,          // "expiry" | "drift"
    string SubjectRef,    // authId or policyRef
    string Text,
    DateOnly RaisedOn);

/// <summary>ExpiryMath result.</summary>
public sealed record ExpiryResult(int DaysOfMargin, EarlyWarning? Warning);

/// <summary>PolicyDiff result.</summary>
public sealed record DriftResult(
    IReadOnlyList<string> AddedCriteria,
    IReadOnlyList<string> RemovedCriteria,
    EarlyWarning? Warning);

/// <summary>The Gate's verdict — deterministic, shown with its working.</summary>
public sealed record GateDecision(
    bool AutoSubmit,
    string Reason)
{
    public string Route => AutoSubmit ? "auto-submit" : "human-review";
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
    GateDecision? Gate,
    SubmissionDraft? Draft)
{
    /// <summary>True when needs-auth resolved to "not required" — nothing further ran.</summary>
    public bool StoppedEarly => !NeedsAuth.AuthRequired;
}
