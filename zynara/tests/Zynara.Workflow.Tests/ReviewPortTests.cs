using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Authority;
using Zynara.Core.Demo;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;
using Zynara.Core.Submission;
using Zynara.Core.View;

namespace Zynara.Workflow.Tests;

/// <summary>
/// Phase 3 — the human-in-the-loop <c>review</c> port. The workflow assembles the
/// case, persists it, then pauses; the "host" answers with a
/// <see cref="ReviewDecision"/> and the workflow records it (and, on approve-send,
/// hands the case to the Submission Adapter).
/// </summary>
public class ReviewPortTests
{
    private sealed record Kit(
        Microsoft.Agents.AI.Workflows.Workflow Wf, CaseService Cases,
        SubmissionService Submissions, ISubmissionStore SubStore);

    private static Kit Build()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();
        var s = sp.CreateScope().ServiceProvider;

        var wf = CareApprovalWorkflow.BuildDurable(
            s.GetRequiredService<AuthPipeline>(),
            s.GetRequiredService<CaseService>(),
            s.GetRequiredService<SubmissionService>());

        return new Kit(wf, s.GetRequiredService<CaseService>(),
            s.GetRequiredService<SubmissionService>(), s.GetRequiredService<ISubmissionStore>());
    }

    private static Request Scenario(string id) => DemoCatalog.All.Single(x => x.Id == id).Request;

    [Fact]
    public async Task A_review_route_pauses_at_the_port_and_records_the_decision_on_resume()
    {
        var kit = Build();
        var request = Scenario("demo-appeal");   // routes to HumanReview

        await using var run = await InProcessExecution.RunAsync(kit.Wf, request);

        // the workflow paused, asking for a review
        var ask = run.NewEvents.OfType<RequestInfoEvent>().Single();
        Assert.True(ask.Request.TryGetDataAs<ReviewCard>(out var card));
        Assert.Equal("demo-appeal", card!.RequestId);
        Assert.Equal("HumanReview", card.Route);

        // and the case is already in the queue
        Assert.NotNull(await kit.Cases.GetAsync("demo-appeal"));

        // the host answers — a Senior reviewer approves the appeal
        var response = ask.Request.CreateResponse(
            new ReviewDecision("approve-send", "alex@zynara", ReviewerRole.SeniorReviewer, "precedents solid"));
        await run.ResumeAsync([response]);

        var record = await kit.Cases.GetAsync("demo-appeal");
        Assert.Equal("approve-send", record!.Decision!.Action);
        Assert.Equal(ReviewerRole.SeniorReviewer, record.Decision.Role);

        var submission = await kit.SubStore.GetAsync("demo-appeal");
        Assert.Equal(SubmissionStatus.Submitted, submission!.Status);
    }

    [Fact]
    public async Task An_unauthorised_reviewer_is_refused_and_nothing_is_submitted()
    {
        var kit = Build();
        await using var run = await InProcessExecution.RunAsync(kit.Wf, Scenario("demo-appeal"));

        var ask = run.NewEvents.OfType<RequestInfoEvent>().Single();
        var response = ask.Request.CreateResponse(
            new ReviewDecision("approve-send", "coord@zynara", ReviewerRole.Coordinator, null));  // too junior for an appeal
        await run.ResumeAsync([response]);

        var record = await kit.Cases.GetAsync("demo-appeal");
        Assert.Null(record!.Decision);                                   // refused
        Assert.Contains(record.Audit, a => a.Detail.StartsWith("REFUSED"));
        Assert.Null(await kit.SubStore.GetAsync("demo-appeal"));         // not sent
    }

    [Fact]
    public async Task An_auto_submit_route_is_sent_by_the_system_with_no_pause()
    {
        var kit = Build();
        await using var run = await InProcessExecution.RunAsync(kit.Wf, Scenario("demo-ready"));

        Assert.Empty(run.NewEvents.OfType<RequestInfoEvent>());          // never paused

        var submission = await kit.SubStore.GetAsync("demo-ready");
        Assert.Equal(SubmissionStatus.Submitted, submission!.Status);

        var record = await kit.Cases.GetAsync("demo-ready");
        Assert.Equal("approve-send", record!.Decision!.Action);
        Assert.Equal("system", record.Decision.By);
    }
}
