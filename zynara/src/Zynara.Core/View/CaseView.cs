using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.View;

// The reviewer-facing projection of a pipeline run (evaluator P1-1 / P1-2). It
// leads with WHY and the EVIDENCE, not an approve button: every value here is
// already owned by deterministic code, carried through so a human can see the
// working and decide. Zynara.ApiProxy serialises this; the dashboard renders it.

/// <summary>Where the case stands — the Gate route, or "not required" / "blocked".</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CaseStatus
{
    /// <summary>needs-auth resolved to "no prior authorisation required".</summary>
    NotRequired,

    /// <summary>Gap-checked, Critic-cleared, within limits — the draft can be sent.</summary>
    ReadyToSubmit,

    /// <summary>Fixable gaps — back to the clinician for more evidence.</summary>
    NeedsStrengthening,

    /// <summary>A mandatory gap, a contradiction, a value over the limit, or a Critic block.</summary>
    NeedsHumanReview,

    /// <summary>Not enough reliable evidence — the system declined to advise.</summary>
    SystemAbstained,
}

/// <summary>A presentation confidence, <b>derived</b> from the decision model — never a free-standing number.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum ConfidenceLevel
{
    NotApplicable,
    Low,
    Medium,
    High,
}

public sealed record CaseView(
    string RequestId,
    string Procedure,
    string PayerPlan,
    string Region,
    CaseStatus Status,
    string Headline,
    string AiAction,
    NeedsAuthView Auth,
    IReadOnlyList<CriterionEvidenceView> Criteria,
    IReadOnlyList<PrecedentView> Precedents,
    CriticView? Critic,
    IReadOnlyList<string> Conflicts,
    ConfidenceLevel Confidence,
    GateView? Gate,
    string? DraftBody,
    string? AppealDraft,
    IReadOnlyList<HumanControl> Controls,
    string ApproveAuthority,
    IReadOnlyList<AuditView> Audit);

/// <summary>One audit-trail line for the reviewer (evaluator §13).</summary>
public sealed record AuditView(string Kind, string Actor, string At, string Detail);

public sealed record NeedsAuthView(
    bool AuthRequired,
    string? RequiredCode,
    string? PolicyRef,
    bool Ambiguous,
    string Note);

/// <summary>One criterion, with the clinical statement that satisfies it and its source.</summary>
public sealed record CriterionEvidenceView(
    string Id,
    string Text,
    bool Mandatory,
    CriterionStatus Status,
    string? EvidenceStatement,
    string Source,
    string PolicyRef,
    string PolicyVersion);

/// <summary>One comparable precedent — the panel that answers "why should I trust this".</summary>
public sealed record PrecedentView(
    string CaseId,
    double Similarity,
    IReadOnlyList<string> MatchedFacts,
    bool InitiallyDenied,
    bool WonOnAppeal,
    IReadOnlyList<string> ClausesCited,
    DateOnly DecidedOn,
    string Narrative,
    bool DroveRecommendation);

public sealed record CriticView(
    CriticVerdict Verdict,
    IReadOnlyList<CriticFlagView> Flags,
    string Summary);

public sealed record CriticFlagView(string Check, string Concern);

public sealed record GateView(
    GateRoute Route,
    string Reason,
    bool MandatoryPass,
    IReadOnlyList<string> UnmetMandatory,
    int SupportingDocumented,
    int SupportingPartial,
    int SupportingMissing,
    EvidenceQuality EvidenceQuality,
    bool ContradictionDetected,
    PrecedentSupport PrecedentSupport);

/// <summary>One action offered to the reviewer. <see cref="Primary"/> marks the route's expected next step.</summary>
public sealed record HumanControl(string Action, string Label, bool Primary);
