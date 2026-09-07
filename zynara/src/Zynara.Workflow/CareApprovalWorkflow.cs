using Microsoft.Agents.AI.Workflows;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Workflow;

/// <summary>
/// PHASE 0 SPIKE — the prior-authorisation pipeline as a MAF Workflow graph.
///
///   Request → needs-auth → evidence-gap → contradiction → precedent-match → critic → gate → (output PipelineState)
///
/// Each executor is a thin lambda over the existing <c>Zynara.Core.Pipeline.*</c>
/// class — the deterministic logic and the agent ports are unchanged. The Gate is
/// a plain executor (never an agent). Phase 1 adds the routing switch + persist +
/// the review port.
/// </summary>
public static class CareApprovalWorkflow
{
    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        NeedsAuthCheck needsAuth,
        EvidenceGapMatch evidenceGap,
        ContradictionCheck contradiction,
        AppealMatch appealMatch,
        CriticCheck critic,
        Gate gate)
    {
        var intake = Step("intake", (Request r) => new PipelineState(r));

        var na = Step("needs-auth", async (PipelineState s) =>
            s with { NeedsAuth = await needsAuth.RunAsync(s.Request) });

        var gap = Step("evidence-gap", async (PipelineState s) =>
            s with { Gap = await evidenceGap.RunAsync(s.Request) });

        var contra = Step("claims-extraction", async (PipelineState s) =>
            s with { Contradiction = await contradiction.RunAsync(s.Request) });

        var precedent = Step("precedent-match", async (PipelineState s) =>
            s with { Appeal = await appealMatch.RunAsync(s.Request, s.Gap!.Assessment) });

        var criticStep = Step("critic", async (PipelineState s) =>
            s with { Critic = await critic.RunAsync(s.Request, s.NeedsAuth!, s.Gap!, s.Appeal!, s.Contradiction) });

        var gateStep = Step("gate", (PipelineState s) =>
        {
            var isAppeal = !string.IsNullOrWhiteSpace(s.Request.DenialLetter);
            var decision = gate.Evaluate(s.Gap!, s.Appeal, s.Request.EstimatedValue, s.Critic, isAppeal, s.Contradiction);
            return s with { Decision = decision };
        });

        return new WorkflowBuilder(intake)
            .WithName("CareApprovalPipeline")
            .AddEdge(intake, na)
            .AddEdge(na, gap)
            .AddEdge(gap, contra)
            .AddEdge(contra, precedent)
            .AddEdge(precedent, criticStep)
            .AddEdge(criticStep, gateStep)
            .WithOutputFrom(gateStep)
            .Build();
    }

    // --- helpers: bind a plain function as a workflow executor --------------
    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, ValueTask<TOut>> handler) =>
        handler.BindAsExecutor(id);

    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, TOut> handler) =>
        new Func<TIn, ValueTask<TOut>>(x => new ValueTask<TOut>(handler(x))).BindAsExecutor(id);
}
