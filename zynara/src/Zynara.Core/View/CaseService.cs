using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Authority;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Core.View;

/// <summary>
/// Runs a request through the pipeline, projects it to a <see cref="CaseView"/>,
/// and stores the <see cref="CaseRecord"/> with its audit trail — the unit
/// <c>Zynara.ApiProxy</c> calls. Read paths and the reviewer decision (with the
/// authority check) go through here too, so the API functions stay thin and the
/// whole flow is unit-tested in Core.
/// </summary>
public sealed class CaseService(
    AuthPipeline pipeline,
    IZynaraStore store,
    ICaseRepository cases,
    IAgentRoster? roster = null,
    TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<CaseRecord> RunAsync(Request request, CancellationToken ct = default)
    {
        var result = await pipeline.RunAsync(request, ct);
        var criteria = await store.GetCriteriaAsync(
            request.PayerPlan, request.Procedure, request.Region, ct);

        var now = _clock.GetUtcNow();

        var audit = new List<AuditEntry>
        {
            new("assembled", "system", now,
                $"pipeline ran → {(result.Gate is { } g ? g.Route.ToString() : "not required")}"),
        };
        foreach (var a in RanAgents(result))
            audit.Add(new AuditEntry("agent", a.Label, now, "invoked during assembly"));

        var view = CaseViewBuilder.Build(result, request, criteria, audit);
        var record = new CaseRecord(request.Id, request, result, view, now) { Audit = audit };

        await cases.SaveAsync(record, ct);
        return record;
    }

    public Task<CaseRecord?> GetAsync(string requestId, CancellationToken ct = default) =>
        cases.GetAsync(requestId, ct);

    public Task<IReadOnlyList<CaseRecord>> ListAsync(CancellationToken ct = default) =>
        cases.ListAsync(ct);

    /// <summary>
    /// Records a reviewer decision. Enforces <see cref="ApprovalAuthority"/> — a role
    /// without the authority for this route / value / appeal is refused, and the
    /// refusal itself is written to the audit trail.
    /// </summary>
    public async Task<DecisionOutcome> RecordDecisionAsync(
        string requestId, string action, string? by, ReviewerRole role, string? note,
        CancellationToken ct = default)
    {
        var record = await cases.GetAsync(requestId, ct);
        if (record is null) return DecisionOutcome.NotFound;

        var route = record.Result.Gate?.Route ?? GateRoute.HumanReview;
        var isAppeal = ApprovalAuthority.IsAppeal(!string.IsNullOrWhiteSpace(record.Request.DenialLetter));
        var check = ApprovalAuthority.Check(role, action, route, record.Request.EstimatedValue, isAppeal);

        var now = _clock.GetUtcNow();
        var actor = $"reviewer:{by ?? "unknown"} ({role})";
        var audit = record.Audit.ToList();

        if (!check.Allowed)
        {
            audit.Add(new AuditEntry("decision", actor, now, $"REFUSED {action} — {check.Reason}"));
            await cases.SaveAsync(record with { Audit = audit }, ct);
            return DecisionOutcome.Forbidden(check);
        }

        audit.Add(new AuditEntry("decision", actor, now,
            $"{action}" + (string.IsNullOrWhiteSpace(note) ? "" : $" — “{note}”")));

        var criteria = await store.GetCriteriaAsync(
            record.Request.PayerPlan, record.Request.Procedure, record.Request.Region, ct);

        var updated = record with
        {
            Decision = new ReviewerDecision(action, by, role, now, note),
            Audit = audit,
            View = CaseViewBuilder.Build(record.Result, record.Request, criteria, audit),
        };
        await cases.SaveAsync(updated, ct);
        return DecisionOutcome.Ok(updated);
    }

    private IEnumerable<AgentAttribution> RanAgents(PipelineResult result)
    {
        if (roster is null) yield break;
        foreach (var a in roster.Describe())
        {
            if (a.Step == "needs-auth") { yield return a; continue; }
            if (!result.StoppedEarly) yield return a;
        }
    }
}

/// <summary>Result of a decision attempt — success with the updated record, or a refusal with the reason.</summary>
public sealed record DecisionOutcome(bool Found, bool Allowed, CaseRecord? Record, AuthorityCheck? Refusal)
{
    public static readonly DecisionOutcome NotFound = new(false, false, null, null);
    public static DecisionOutcome Ok(CaseRecord record) => new(true, true, record, null);
    public static DecisionOutcome Forbidden(AuthorityCheck check) => new(true, false, null, check);
}
