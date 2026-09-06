using Microsoft.Azure.Functions.Worker;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Orchestrator.Orchestration;

/// <summary>
/// One Activity Function per deterministic spoke (TDD §12 · TD-2). Each is a thin
/// adapter over the matching <c>Zynara.Core.Pipeline</c> class — the activity
/// boundary buys per-step retry isolation, a per-step stub-twin in CI, and a
/// replay-safe resume point. No decision logic lives here.
/// </summary>
public sealed class SpokeActivities(
    NeedsAuthCheck needsAuth,
    EvidenceGapMatch evidenceGap,
    AppealMatch appealMatch,
    CriticCheck critic,
    IZynaraStore store)
{
    [Function(nameof(NeedsAuthActivity))]
    public Task<NeedsAuthResult> NeedsAuthActivity([ActivityTrigger] Request request) =>
        needsAuth.RunAsync(request);

    [Function(nameof(EvidenceGapActivity))]
    public Task<EvidenceGapResult> EvidenceGapActivity([ActivityTrigger] Request request) =>
        evidenceGap.RunAsync(request);

    [Function(nameof(AppealMatchActivity))]
    public Task<AppealMatchResult> AppealMatchActivity([ActivityTrigger] AppealMatchInput input) =>
        appealMatch.RunAsync(input.Request, input.Assessment);

    [Function(nameof(CriticActivity))]
    public Task<CriticReview> CriticActivity([ActivityTrigger] CriticInput input) =>
        critic.RunAsync(input.Request, input.NeedsAuth, input.Gap, input.Appeal);

    [Function(nameof(DraftActivity))]
    public async Task<SubmissionDraft> DraftActivity([ActivityTrigger] DraftInput input)
    {
        var criteria = await store.GetCriteriaAsync(
            input.Request.PayerPlan, input.Request.Procedure, input.Request.Region);

        return DraftBuilder.Build(input.Request, input.NeedsAuth, input.Gap, input.Appeal, criteria);
    }
}
