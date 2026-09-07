using Microsoft.Agents.AI.Workflows;
using Zynara.Core.Abstractions;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Workflow;

/// <summary>
/// Runs the <see cref="CareApprovalWorkflow"/> and returns the assembled
/// <see cref="PipelineResult"/> — the workflow equivalent of
/// <c>Zynara.Core.Pipeline.AuthPipeline</c>. Same signature so callers
/// (<c>CaseService</c>, the eval harness) swap one for the other.
/// </summary>
public sealed class CareApprovalRunner(
    IZynaraStore store,
    NeedsAuthCheck needsAuth,
    EvidenceGapMatch evidenceGap,
    ContradictionCheck contradiction,
    AppealMatch appealMatch,
    CriticCheck critic,
    Gate gate)
{
    public async Task<PipelineResult> RunAsync(Request request, CancellationToken ct = default)
    {
        var wf = CareApprovalWorkflow.Build(store, needsAuth, evidenceGap, contradiction, appealMatch, critic, gate);

        await using var run = await InProcessExecution.RunAsync(wf, request, cancellationToken: ct);

        var output = run.NewEvents.OfType<WorkflowOutputEvent>().LastOrDefault()
            ?? throw new InvalidOperationException($"CareApproval workflow produced no output for {request.Id}.");

        return output.As<PipelineResult>()
            ?? throw new InvalidOperationException($"CareApproval workflow output was not a PipelineResult for {request.Id}.");
    }
}
