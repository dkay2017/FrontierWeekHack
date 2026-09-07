using Microsoft.Azure.Functions.Worker;
using Microsoft.DurableTask;
using Zynara.Core.Agents;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Orchestrator.Orchestration;

/// <summary>
/// The deterministic hub (TDD §12 · TD-1). It sequences the spoke activities and
/// owns the Gate — the routing decision is computed here, in the orchestrator, not
/// delegated to an activity or an agent. Mirrors
/// <see cref="Zynara.Core.Pipeline.AuthPipeline"/> step for step; that class stays
/// the single-process reference the tests exercise.
/// </summary>
public sealed class AuthOrchestrator(Gate gate)
{
    /// <summary>Each spoke gets 3 attempts with a 2s → 4s → 8s backoff.</summary>
    private static readonly TaskOptions Retry = TaskOptions.FromRetryPolicy(
        new RetryPolicy(maxNumberOfAttempts: 3, firstRetryInterval: TimeSpan.FromSeconds(2), backoffCoefficient: 2.0));

    [Function(nameof(AuthOrchestrator))]
    public async Task<PipelineResult> Run([OrchestrationTrigger] TaskOrchestrationContext context)
    {
        var request = context.GetInput<Request>()
            ?? throw new InvalidOperationException("AuthOrchestrator started with no request.");

        var needsAuth = await context.CallActivityAsync<NeedsAuthResult>(
            nameof(SpokeActivities.NeedsAuthActivity), request, Retry);

        // Unambiguous "not required" is the only path that stops the pipeline.
        if (!needsAuth.AuthRequired)
        {
            var stopped = new PipelineResult(request.Id, needsAuth, null, null, null, null, null);
            await context.CallActivityAsync(
                nameof(SpokeActivities.PersistCaseActivity), new PersistInput(request, stopped), Retry);
            return stopped;
        }

        var gap = await context.CallActivityAsync<EvidenceGapResult>(
            nameof(SpokeActivities.EvidenceGapActivity), request, Retry);

        var conflict = await context.CallActivityAsync<ContradictionResult>(
            nameof(SpokeActivities.ContradictionActivity), request, Retry);

        var appeal = await context.CallActivityAsync<AppealMatchResult>(
            nameof(SpokeActivities.AppealMatchActivity), new AppealMatchInput(request, gap.Assessment), Retry);

        var critic = await context.CallActivityAsync<CriticReview>(
            nameof(SpokeActivities.CriticActivity), new CriticInput(request, needsAuth, gap, appeal, conflict), Retry);

        // The Gate — deterministic, no I/O, computed in the hub.
        var isAppeal = !string.IsNullOrWhiteSpace(request.DenialLetter);
        var decision = gate.Evaluate(gap, appeal, request.EstimatedValue, critic, isAppeal, conflict);

        var draft = await context.CallActivityAsync<SubmissionDraft>(
            nameof(SpokeActivities.DraftActivity), new DraftInput(request, needsAuth, gap, appeal), Retry);

        var result = new PipelineResult(request.Id, needsAuth, gap, appeal, critic, decision, draft)
        {
            Contradiction = conflict,
        };

        // Persist the assembled case so it shows up in the reviewer queue.
        await context.CallActivityAsync(
            nameof(SpokeActivities.PersistCaseActivity), new PersistInput(request, result), Retry);

        return result;
    }
}
