using Microsoft.Agents.AI.Workflows;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Workflow;

/// <summary>
/// The prior-authorisation pipeline as a Microsoft Agent Framework Workflow.
///
///   Request → intake → needs-auth ─┬─(not required)──────────────→ finalize → out
///                                  └─(required)→ evidence-gap → contradiction
///                                     → precedent-match → critic → gate → finalize → out
///
/// Each step is a thin lambda over the existing <c>Zynara.Core.Pipeline.*</c>
/// class — deterministic logic and agent ports unchanged. The Gate is a plain
/// executor, never an agent (the hybrid principle). <c>finalize</c> assembles the
/// same <see cref="PipelineResult"/> the v1 <c>AuthPipeline</c> produced, so the
/// read model / eval consume it identically. The routing switch + review port +
/// submission edge come in Phase 3.
/// </summary>
public static class CareApprovalWorkflow
{
    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        IZynaraStore store,
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
            return s with { Decision = gate.Evaluate(s.Gap!, s.Appeal, s.Request.EstimatedValue, s.Critic, isAppeal, s.Contradiction) };
        });

        var finalize = Step("finalize", async (PipelineState s) => await FinalizeAsync(store, s));

        return new WorkflowBuilder(intake)
            .WithName("CareApprovalPipeline")
            .AddEdge(intake, na)
            .AddEdge<PipelineState>(na, finalize, s => !s.AuthRequired)
            .AddEdge<PipelineState>(na, gap, s => s.AuthRequired)
            .AddEdge(gap, contra)
            .AddEdge(contra, precedent)
            .AddEdge(precedent, criticStep)
            .AddEdge(criticStep, gateStep)
            .AddEdge(gateStep, finalize)
            .WithOutputFrom(finalize)
            .Build();
    }

    private static async ValueTask<PipelineResult> FinalizeAsync(IZynaraStore store, PipelineState s)
    {
        if (s.NeedsAuth is { AuthRequired: false })
            return new PipelineResult(s.Request.Id, s.NeedsAuth, null, null, null, null, null)
            {
                Metrics = new PipelineMetrics(0, 0, 0, ReasoningStepsRun: 1, 0),
            };

        var criteria = await store.GetCriteriaAsync(s.Request.PayerPlan, s.Request.Procedure, s.Request.Region);
        var draft = DraftBuilder.Build(s.Request, s.NeedsAuth!, s.Gap!, s.Appeal!, criteria);

        var metrics = new PipelineMetrics(
            CriteriaChecked: s.Gap!.Assessment.Findings.Count,
            EvidenceGapsFound: s.Gap.Assessment.Findings.Count(f => f.Status != CriterionStatus.Documented),
            PrecedentsConsidered: s.Appeal!.Shortlist.Count,
            ReasoningStepsRun: 5,
            AssembledInMs: 0);

        return new PipelineResult(s.Request.Id, s.NeedsAuth!, s.Gap, s.Appeal, s.Critic, s.Decision, draft)
        {
            Metrics = metrics,
            Contradiction = s.Contradiction,
        };
    }

    // --- helpers: bind a plain function as a workflow executor --------------
    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, ValueTask<TOut>> handler) =>
        handler.BindAsExecutor(id);

    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, TOut> handler) =>
        new Func<TIn, ValueTask<TOut>>(x => new ValueTask<TOut>(handler(x))).BindAsExecutor(id);
}
