using Zynara.Core.Authority;
using Zynara.Core.View;

namespace Zynara.Core.Model;

/// <summary>
/// One assembled case as it is stored and read back: the request, the full
/// pipeline result, the reviewer projection, the append-only audit trail, and —
/// once a human has acted — their decision. <c>Zynara.Data</c> will persist this
/// to Cosmos (partition <c>/requestId</c>, an <c>audit/</c> sub-collection); until
/// then it lives in <see cref="Abstractions.ICaseRepository"/>.
/// </summary>
public sealed record CaseRecord(
    string RequestId,
    Request Request,
    PipelineResult Result,
    CaseView View,
    DateTimeOffset RunAt,
    ReviewerDecision? Decision = null)
{
    /// <summary>Append-only. Who / what touched this case and when — agent runs (with version) + reviewer actions.</summary>
    public IReadOnlyList<AuditEntry> Audit { get; init; } = Array.Empty<AuditEntry>();
}

/// <summary>One line in the case audit trail (evaluator §13).</summary>
public sealed record AuditEntry(
    string Kind,          // "assembled" | "agent" | "decision"
    string Actor,         // "system" | "evidence-gap · foundry v3" | "reviewer:alex@zynara (SeniorReviewer)"
    DateTimeOffset At,
    string Detail);

/// <summary>A reviewer's action on a case — identity, role, timestamp, note (evaluator §13).</summary>
public sealed record ReviewerDecision(
    string Action,
    string? By,
    ReviewerRole? Role,
    DateTimeOffset At,
    string? Note);

/// <summary>The list-view shape — enough for the review queue without the full projection.</summary>
public sealed record CaseSummary(
    string RequestId,
    string Procedure,
    string PayerPlan,
    string Region,
    CaseStatus Status,
    string Headline,
    ConfidenceLevel Confidence,
    DateTimeOffset RunAt,
    string? DecisionAction)
{
    public static CaseSummary From(CaseRecord r) => new(
        r.RequestId, r.View.Procedure, r.View.PayerPlan, r.View.Region,
        r.View.Status, r.View.Headline, r.View.Confidence, r.RunAt, r.Decision?.Action);
}
