using System.Diagnostics;
using System.Text;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Diagnostics;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// The prior-authorisation sequence as pure code:
/// <c>needs-auth → evidence gap → appeal match → Critic → Gate → draft</c>.
/// The Durable Functions orchestrator (slice 4) runs the same spokes as activities
/// for retry isolation and replay; this class is the single-process reference used
/// locally and in tests, and the body the orchestrator's steps delegate to.
/// </summary>
public sealed class AuthPipeline(
    NeedsAuthCheck needsAuth,
    EvidenceGapMatch evidenceGap,
    ContradictionCheck contradiction,
    AppealMatch appealMatch,
    CriticCheck critic,
    Gate gate,
    IZynaraStore store)
{
    public async Task<PipelineResult> RunAsync(Request request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        using var run = ZynaraTelemetry.StartPipelineRun(request);

        var na = await needsAuth.RunAsync(request, ct);
        if (!na.AuthRequired)
        {
            ZynaraTelemetry.RecordRoute(run, GateRoute.ReadyToSubmit, reasoningSteps: 1);
            run?.SetTag("zynara.stopped_early", true);
            return new PipelineResult(request.Id, na, null, null, null, null, null)
            {
                Metrics = new PipelineMetrics(0, 0, 0, ReasoningStepsRun: 1, sw.ElapsedMilliseconds),
            };
        }

        var gap = await evidenceGap.RunAsync(request, ct);
        var conflict = await contradiction.RunAsync(request, ct);
        var appeal = await appealMatch.RunAsync(request, gap.Assessment, ct);
        var review = await critic.RunAsync(request, na, gap, appeal, conflict, ct);
        var isAppeal = !string.IsNullOrWhiteSpace(request.DenialLetter);
        var decision = gate.Evaluate(gap, appeal, request.EstimatedValue, review, isAppeal, conflict);
        ZynaraTelemetry.RecordRoute(run, decision.Route, reasoningSteps: 5);

        var criteria = await store.GetCriteriaAsync(
            request.PayerPlan, request.Procedure, request.Region, ct);

        var draft = DraftBuilder.Build(request, na, gap, appeal, criteria);
        sw.Stop();

        var metrics = new PipelineMetrics(
            CriteriaChecked: gap.Assessment.Findings.Count,
            EvidenceGapsFound: gap.Assessment.Findings.Count(f => f.Status != CriterionStatus.Documented),
            PrecedentsConsidered: appeal.Shortlist.Count,
            ReasoningStepsRun: 5,   // needs-auth · evidence-gap · claims-extraction · precedent-strategist · critic
            AssembledInMs: sw.ElapsedMilliseconds);

        return new PipelineResult(request.Id, na, gap, appeal, review, decision, draft)
        {
            Metrics = metrics,
            Contradiction = conflict,
        };
    }
}

/// <summary>
/// Assembles the reviewer-facing <see cref="SubmissionDraft"/> from the spoke
/// outputs. Public because the Durable orchestrator's <c>DraftActivity</c> calls it
/// directly (slice 4) as well as <see cref="AuthPipeline"/>.
/// </summary>
public static class DraftBuilder
{
    public static SubmissionDraft Build(
        Request request,
        NeedsAuthResult na,
        EvidenceGapResult gap,
        AppealMatchResult appeal,
        Criteria? criteria)
    {
        var documented = gap.Assessment
            .WithStatus(CriterionStatus.Documented)
            .Select(id => criteria?.Items.FirstOrDefault(c => c.Id == id)?.Text)
            .Where(t => t is not null).Cast<string>().ToArray();

        var missing = gap.Assessment
            .WithStatus(CriterionStatus.Missing).Concat(gap.Assessment.WithStatus(CriterionStatus.Partial))
            .Select(id => criteria?.Items.FirstOrDefault(c => c.Id == id)?.Text)
            .Where(t => t is not null).Cast<string>().ToArray();

        var body = new StringBuilder()
            .AppendLine($"Prior-authorisation request — {request.Procedure} ({na.RequiredCode ?? "code TBC"})")
            .AppendLine($"Payer / plan: {request.PayerPlan}  ·  Region: {request.Region}  ·  Policy: {na.PolicyRef}")
            .AppendLine()
            .AppendLine($"Mandatory criteria: {(gap.MandatoryPass ? "all met" : $"UNMET — {string.Join(", ", gap.UnmetMandatory)}")}")
            .AppendLine($"Supporting criteria documented ({gap.SupportingDocumented}/" +
                        $"{gap.SupportingDocumented + gap.SupportingPartial + gap.SupportingMissing}):")
            .AppendLine(documented.Length > 0 ? string.Join("\n", documented.Select(c => $"  - {c}")) : "  (none)")
            .AppendLine()
            .AppendLine("Clinical summary:")
            .AppendLine("  " + Truncate(request.ClinicalNote, 600))
            .ToString();

        if (missing.Length > 0 || gap.ContradictionDetected)
            body += $"\nOutstanding for the reviewer: {missing.Length} undocumented" +
                    (gap.ContradictionDetected ? ", contradictory evidence present" : "") + ".\n";

        body += $"\nAppeal analysis: {appeal.Recommendation.Text} (precedent support: {appeal.Support}).";

        return new SubmissionDraft(
            RequestId: request.Id,
            PayerPlan: request.PayerPlan,
            Procedure: request.Procedure,
            RequiredCode: na.RequiredCode,
            Body: body.ReplaceLineEndings("\n").TrimEnd(),
            CitedCriteria: documented,
            CitedPrecedents: appeal.Recommendation.CitedPrecedentIds);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
