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
            .AddEdge<PipelineState>(na, finalize, s => !s.AuthRequired)
            .AddEdge<PipelineState>(na, gap, s => s.AuthRequired)
            .AddEdge(gap, contra).AddEdge(contra, precedent).AddEdge(precedent, criticStep)
            .AddEdge(criticStep, gateStep).AddEdge(gateStep, finalize)
            .WithOutputFrom(finalize)
            .Build();
    }

    /// <summary>
    /// The full flow with the human-in-the-loop <c>review</c> port. After the Gate
    /// the case is persisted; an AutoSubmit route is sent by the system, every
    /// other route pauses at the port until the host answers
    /// (<c>/respond/{runId}</c>) with a <see cref="ReviewDecision"/>.
    /// </summary>
    public static Microsoft.Agents.AI.Workflows.Workflow BuildWithReview(
        IZynaraStore store, CaseService cases, SubmissionService submissions,
        NeedsAuthCheck needsAuth, EvidenceGapMatch evidenceGap, ContradictionCheck contradiction,
        AppealMatch appealMatch, CriticCheck critic, Gate gate)
    {
        var (intake, na, gap, contra, precedent, criticStep, gateStep) =
            Spokes(needsAuth, evidenceGap, contradiction, appealMatch, critic, gate);

        var persist = Step("persist", async (PipelineState s, IWorkflowContext ctx) =>
        {
            var result = ToResult(await store.GetCriteriaAsync(s.Request.PayerPlan, s.Request.Procedure, s.Request.Region), s);
            await cases.PersistAsync(s.Request, result);
            await ctx.QueueStateUpdateAsync(ReqIdKey, s.Request.Id, SharedScope);
            return s;
        });

        var autoApprove = Step("auto-approve", async (PipelineState s) =>
        {
            await cases.RecordDecisionAsync(s.Request.Id, "approve-send", "system", ReviewerRole.Coordinator, "auto-submit");
            await submissions.SubmitAsync(s.Request.Id);
            return s.Request.Id;
        });

        var toCard = Step("review-card", (PipelineState s) =>
            new ReviewCard(s.Request.Id, s.Request.Procedure, s.Request.PayerPlan, s.Decision!.Route.ToString(),
                s.Gap is null ? "not required" : $"{s.Gap.Assessment.Summary}"));

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
            .AddEdge(intake, na)
            .AddEdge<PipelineState>(na, persist, s => !s.AuthRequired)
            .AddEdge<PipelineState>(na, gap, s => s.AuthRequired)
            .AddEdge(gap, contra).AddEdge(contra, precedent).AddEdge(precedent, criticStep)
            .AddEdge(criticStep, gateStep).AddEdge(gateStep, persist)
            .AddEdge<PipelineState>(persist, autoApprove, s => s.Decision?.Route == GateRoute.AutoSubmit)
            .AddEdge<PipelineState>(persist, toCard, s => s.Decision is { Route: not GateRoute.AutoSubmit });

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
