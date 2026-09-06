using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core.Abstractions;
using Zynara.Core.Authority;
using Zynara.Core.Demo;
using Zynara.Core.Submission;
using Zynara.Core.View;

namespace Zynara.Core.Tests;

/// <summary>
/// The Submission Adapter (S-1). It only ever sends a case a reviewer approved,
/// and never sends the same request twice.
/// </summary>
public class SubmissionServiceTests
{
    private static ServiceProvider Build() => new ServiceCollection()
        .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
        .AddZynaraAgents()
        .AddZynaraCore()
        .BuildServiceProvider();

    private static async Task ApproveAsync(ServiceProvider sp, string scenarioId, ReviewerRole role)
    {
        var cases = sp.GetRequiredService<CaseService>();
        await cases.RunAsync(DemoCatalog.All.Single(s => s.Id == scenarioId).Request);
        var outcome = await cases.RecordDecisionAsync(scenarioId, "approve-send", "reviewer@zynara", role, null);
        Assert.True(outcome.Allowed);
    }

    [Fact]
    public async Task An_approved_case_is_sent_and_recorded()
    {
        using var sp = Build();
        await ApproveAsync(sp, "demo-ready", ReviewerRole.Reviewer);

        var outcome = await sp.GetRequiredService<SubmissionService>().SubmitAsync("demo-ready");

        Assert.True(outcome.Sent);
        Assert.Equal(SubmissionResultKind.Submitted, outcome.Kind);
        Assert.Equal("ACK-demo-ready", outcome.Record!.PayerReference);
        Assert.Equal("reviewer@zynara", outcome.Record.ApprovedBy);
        Assert.Equal("stub", outcome.Record.Channel);
    }

    [Fact]
    public async Task A_second_submit_returns_the_existing_record_and_does_not_resend()
    {
        using var sp = Build();
        await ApproveAsync(sp, "demo-ready", ReviewerRole.Reviewer);
        var svc = sp.GetRequiredService<SubmissionService>();

        var first = await svc.SubmitAsync("demo-ready");
        var second = await svc.SubmitAsync("demo-ready");

        Assert.Equal(SubmissionResultKind.Submitted, second.Kind);
        Assert.Equal(first.Record!.SubmittedAt, second.Record!.SubmittedAt);   // same record, not a new send
        Assert.Single(await svc.ListAsync());
    }

    [Fact]
    public async Task An_unapproved_case_is_not_sent()
    {
        using var sp = Build();
        await sp.GetRequiredService<CaseService>()
            .RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-strengthen").Request);

        var outcome = await sp.GetRequiredService<SubmissionService>().SubmitAsync("demo-strengthen");

        Assert.Equal(SubmissionResultKind.NotApproved, outcome.Kind);
        Assert.Null(await sp.GetRequiredService<SubmissionService>().GetAsync("demo-strengthen"));
    }

    [Fact]
    public async Task An_unknown_request_is_reported_not_found()
    {
        using var sp = Build();

        var outcome = await sp.GetRequiredService<SubmissionService>().SubmitAsync("no-such-request");

        Assert.Equal(SubmissionResultKind.CaseNotFound, outcome.Kind);
    }

    [Fact]
    public async Task A_failing_gateway_records_a_failed_submission()
    {
        using var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
            .AddZynaraAgents()
            .AddZynaraCore()
            .AddSingleton<IPayerGateway, ThrowingGateway>()
            .BuildServiceProvider();
        await ApproveAsync(sp, "demo-ready", ReviewerRole.Reviewer);

        var outcome = await sp.GetRequiredService<SubmissionService>().SubmitAsync("demo-ready");

        Assert.Equal(SubmissionResultKind.Failed, outcome.Kind);
        Assert.Equal(Model.SubmissionStatus.Failed, outcome.Record!.Status);
    }

    private sealed class ThrowingGateway : IPayerGateway
    {
        public string Channel => "test-throwing";
        public Task<PayerAck> SendAsync(Model.SubmissionDraft draft, string payerPlan, CancellationToken ct = default) =>
            throw new InvalidOperationException("payer intake unavailable");
    }
}
