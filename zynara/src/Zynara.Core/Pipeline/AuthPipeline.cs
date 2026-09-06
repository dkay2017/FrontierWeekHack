using Zynara.Core.Abstractions;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// The prior-authorisation sequence as pure code:
/// <c>needs-auth → evidence gap → appeal match → Gate → draft</c>.
/// The Durable Functions orchestrator (slice 4) runs the same spokes as activities
/// for retry isolation and replay; this class is the single-process reference used
/// locally and in tests, and the body the orchestrator's steps delegate to.
/// </summary>
public sealed class AuthPipeline(
    NeedsAuthCheck needsAuth,
    EvidenceGapMatch evidenceGap,
    AppealMatch appealMatch,
    Gate gate,
    IZynaraStore store)
{
    public async Task<PipelineResult> RunAsync(Request request, CancellationToken ct = default)
    {
        var na = await needsAuth.RunAsync(request, ct);
        if (!na.AuthRequired)
            return new PipelineResult(request.Id, na, null, null, null, null);

        var gap = await evidenceGap.RunAsync(request, ct);
        var appeal = await appealMatch.RunAsync(request, gap.Assessment, ct);
        var decision = gate.Evaluate(gap, request.EstimatedValue);

        var criteria = await store.GetCriteriaAsync(
            request.PayerPlan, request.Procedure, request.Region, ct);

        var draft = DraftBuilder.Build(request, na, gap, appeal, criteria);

        return new PipelineResult(request.Id, na, gap, appeal, decision, draft);
    }
}

internal static class DraftBuilder
{
    public static SubmissionDraft Build(
        Request request,
        NeedsAuthResult na,
        EvidenceGapResult gap,
        AppealMatchResult appeal,
        Criteria? criteria)
    {
        var citedCriteria = criteria is null
            ? Array.Empty<string>()
            : gap.Assessment.Met
                .Select(id => criteria.Items.FirstOrDefault(c => c.Id == id)?.Text)
                .Where(t => t is not null)
                .Cast<string>()
                .ToArray();

        var body = new System.Text.StringBuilder()
            .AppendLine($"Prior-authorisation request — {request.Procedure} ({na.RequiredCode ?? "code TBC"})")
            .AppendLine($"Payer / plan: {request.PayerPlan}  ·  Region: {request.Region}  ·  Policy: {na.PolicyRef}")
            .AppendLine()
            .AppendLine($"Criteria documented ({gap.Assessment.Met.Count}/{(criteria?.Items.Count ?? 0)}):")
            .AppendLine(citedCriteria.Length > 0
                ? string.Join("\n", citedCriteria.Select(c => $"  - {c}"))
                : "  (none)")
            .AppendLine()
            .AppendLine("Clinical summary:")
            .AppendLine("  " + Truncate(request.ClinicalNote, 600))
            .ToString();

        if (gap.HasGaps)
            body += $"\nOutstanding: {gap.Assessment.Missing.Count} missing, " +
                    $"{gap.Assessment.Conflicts.Count} contradicted — see the reviewer notes.\n";

        body += $"\n{appeal.Recommendation.Text}";

        return new SubmissionDraft(
            RequestId: request.Id,
            PayerPlan: request.PayerPlan,
            Procedure: request.Procedure,
            RequiredCode: na.RequiredCode,
            Body: body.TrimEnd(),
            CitedCriteria: citedCriteria,
            CitedPrecedents: appeal.Recommendation.CitedPrecedentIds);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
