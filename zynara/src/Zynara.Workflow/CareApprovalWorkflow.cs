using Microsoft.Agents.AI.Workflows;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Authority;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;
using Zynara.Core.Submission;
using Zynara.Core.View;

namespace Zynara.Workflow;

/// <summary>
/// The prior-authorisation pipeline as a Microsoft Agent Framework Workflow.
/// Each reasoning step is a thin lambda over the existing <c>Zynara.Core.*</c>
/// class — deterministic logic and agent ports unchanged. The Gate is a plain
/// executor, never an agent (the hybrid principle).
/// </summary>
public static class CareApprovalWorkflow
{
    private const string ReqIdKey = "request_id";
    private const string SharedScope = "zynara";   // shared state scope — visible across executors

    /// <summary>
    /// The assembly-only graph (no human step) — produces the same
    /// <see cref="PipelineResult"/> the v1 <c>AuthPipeline</c> did. Used by the
    /// eval harness and the api-proxy scenario runner.
    ///
    ///   intake → needs-auth ─┬(not required)→ finalize
    ///                        └(required)→ evidence-gap → contradiction → precedent-match → critic → gate → finalize
    /// </summary>
    public static Microsoft.Agents.AI.Workflows.Workflow Build(
        IZynaraStore store,
        NeedsAuthCheck needsAuth, EvidenceGapMatch evidenceGap, ContradictionCheck contradiction,
        AppealMatch appealMatch, CriticCheck critic, Gate gate)
    {
        var (intake, na, gap, contra, precedent, criticStep, gateStep) =
            Spokes(needsAuth, evidenceGap, contradiction, appealMatch, critic, gate);

        var finalize = Step("finalize", async (PipelineState s) =>
            ToResult(await store.GetCriteriaAsync(s.Request.PayerPlan, s.Request.Procedure, s.Request.Region), s));

        return new WorkflowBuilder(intake)
            .WithName("CareApprovalAssembly")
            .AddEdge(intake, na)
            .AddEdge<PipelineState>(na, finalize, s => s is { AuthRequired: false })
            .AddEdge<PipelineState>(na, gap, s => s is { AuthRequired: true })
            .AddEdge(gap, contra).AddEdge(contra, precedent).AddEdge(precedent, criticStep)
            .AddEdge(criticStep, gateStep).AddEdge(gateStep, finalize)
            .WithOutputFrom(finalize)
            .Build();
    }

    /// <summary>
    /// The full flow with the human-in-the-loop <c>review</c> port, sized for the
    /// <b>durable</b> host. Durable Task caps the serialised workflow snapshot at
    /// 16&nbsp;KB, so the graph is coarse: <c>assemble</c> runs the whole
    /// reasoning pipeline in-process and persists the <see cref="PipelineResult"/>
    /// to the case store; the message from there on is the tiny <see cref="Flow"/>
    /// (request + route). An AutoSubmit route is sent by the system; every other
    /// route pauses at the port until the host answers <c>/respond/{runId}</c>.
    ///
    ///   Request → intake → assemble ─┬(AutoSubmit)→ auto-approve → submit
    ///                                └(else)→ review-card → PORT ─→ apply-decision
    ///                                                               └(approve-send, authorised)→ submit
    ///
    /// The fine-grained per-step graph (<see cref="Build"/>) is kept for
    /// in-process runs and the tests — that is where the per-step retry / stub /
    /// route-agreement coverage lives.
    /// </summary>
    public static Microsoft.Agents.AI.Workflows.Workflow BuildDurable(
        AuthPipeline pipeline, CaseService cases, SubmissionService submissions)
    {
        var intake = Step("intake", (Request r) => new Flow(r));

        var assemble = Step("assemble", async (Flow f, IWorkflowContext ctx) =>
        {
            var result = await pipeline.RunAsync(f.Request);
            await cases.PersistAsync(f.Request, result);
            await ctx.QueueStateUpdateAsync(ReqIdKey, f.Request.Id, SharedScope);
            return f with
            {
                AuthRequired = !result.StoppedEarly,
                Route = result.Gate?.Route ?? GateRoute.AutoSubmit,
            };
        });

        var autoApprove = Step("auto-approve", async (Flow f) =>
        {
            await cases.RecordDecisionAsync(f.Request.Id, "approve-send", "system", ReviewerRole.Coordinator, "auto-submit");
            await submissions.SubmitAsync(f.Request.Id);
            return f.Request.Id;
        });

        var toCard = Step("review-card", (Flow f) =>
            new ReviewCard(f.Request.Id, f.Request.Procedure, f.Request.PayerPlan, f.Route.ToString(),
                f.AuthRequired ? "assembled — awaiting a reviewer" : "prior authorisation not required"));

        var applyDecision = Step("apply-decision", async (ReviewDecision d, IWorkflowContext ctx) =>
        {
            var reqId = await ctx.ReadStateAsync<string>(ReqIdKey, SharedScope) ?? "";
            var outcome = await cases.RecordDecisionAsync(reqId, d.Action, d.By, d.Role, d.Note);
            if (outcome is { Allowed: true } && d.Action == "approve-send")
                await submissions.SubmitAsync(reqId);
            return reqId;
        });

        var builder = new WorkflowBuilder(intake)
            .WithName("CareApprovalPipeline")
            .AddEdge(intake, assemble)
            .AddEdge<Flow>(assemble, autoApprove, f => f is { Route: GateRoute.AutoSubmit })
            .AddEdge<Flow>(assemble, toCard, f => f is not null && f.Route != GateRoute.AutoSubmit);

        builder.AddExternalCall<ReviewCard, ReviewDecision>(toCard, "review");
        builder.AddEdge("review", applyDecision);

        return builder.WithOutputFrom(autoApprove, applyDecision).Build();
    }

    // --- the shared reasoning spine --------------------------------------------
    private static (ExecutorBinding intake, ExecutorBinding na, ExecutorBinding gap, ExecutorBinding contra,
                    ExecutorBinding precedent, ExecutorBinding critic, ExecutorBinding gate) Spokes(
        NeedsAuthCheck needsAuth, EvidenceGapMatch evidenceGap, ContradictionCheck contradiction,
        AppealMatch appealMatch, CriticCheck critic, Gate gate) =>
    (
        Step("intake", (Request r) => new PipelineState(r)),
        Step("needs-auth", async (PipelineState s) => s with { NeedsAuth = await needsAuth.RunAsync(s.Request) }),
        Step("evidence-gap", async (PipelineState s) => s with { Gap = await evidenceGap.RunAsync(s.Request) }),
        Step("claims-extraction", async (PipelineState s) => s with { Contradiction = await contradiction.RunAsync(s.Request) }),
        Step("precedent-match", async (PipelineState s) => s with { Appeal = await appealMatch.RunAsync(s.Request, s.Gap!.Assessment) }),
        Step("critic", async (PipelineState s) => s with { Critic = await critic.RunAsync(s.Request, s.NeedsAuth!, s.Gap!, s.Appeal!, s.Contradiction) }),
        Step("gate", (PipelineState s) =>
        {
            var isAppeal = !string.IsNullOrWhiteSpace(s.Request.DenialLetter);
            return s with { Decision = gate.Evaluate(s.Gap!, s.Appeal, s.Request.EstimatedValue, s.Critic, isAppeal, s.Contradiction) };
        })
    );

    private static PipelineResult ToResult(Criteria? criteria, PipelineState s)
    {
        if (s.NeedsAuth is { AuthRequired: false })
            return new PipelineResult(s.Request.Id, s.NeedsAuth, null, null, null, null, null)
            {
                Metrics = new PipelineMetrics(0, 0, 0, ReasoningStepsRun: 1, 0),
            };

        var draft = DraftBuilder.Build(s.Request, s.NeedsAuth!, s.Gap!, s.Appeal!, criteria);
        var metrics = new PipelineMetrics(
            CriteriaChecked: s.Gap!.Assessment.Findings.Count,
            EvidenceGapsFound: s.Gap.Assessment.Findings.Count(f => f.Status != CriterionStatus.Documented),
            PrecedentsConsidered: s.Appeal!.Shortlist.Count,
            ReasoningStepsRun: 5, AssembledInMs: 0);

        return new PipelineResult(s.Request.Id, s.NeedsAuth!, s.Gap, s.Appeal, s.Critic, s.Decision, draft)
        {
            Metrics = metrics,
            Contradiction = s.Contradiction,
        };
    }

    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, ValueTask<TOut>> handler) => handler.BindAsExecutor(id);
    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, IWorkflowContext, ValueTask<TOut>> handler) => handler.BindAsExecutor(id);
    private static ExecutorBinding Step<TIn, TOut>(string id, Func<TIn, TOut> handler) =>
        new Func<TIn, ValueTask<TOut>>(x => new ValueTask<TOut>(handler(x))).BindAsExecutor(id);
}
