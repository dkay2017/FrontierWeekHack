using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Demo;
using Zynara.Core.View;

namespace Zynara.Core.Tests;

/// <summary>
/// The reviewer projection (P1-1 / P1-2). Also pins the demo scenarios to the
/// routes the demo script depends on — the same guard the eval suite gives the
/// pipeline.
/// </summary>
public class CaseViewTests
{
    private static CaseService BuildService()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();
        return sp.GetRequiredService<CaseService>();
    }

    [Theory]
    [InlineData("demo-ready", CaseStatus.ReadyToSubmit)]
    [InlineData("demo-strengthen", CaseStatus.NeedsStrengthening)]
    [InlineData("demo-review-mandatory", CaseStatus.NeedsHumanReview)]
    [InlineData("demo-appeal", CaseStatus.NeedsHumanReview)]
    [InlineData("demo-abstain", CaseStatus.SystemAbstained)]
    public async Task Demo_scenarios_reach_their_scripted_status(string id, CaseStatus expected)
    {
        var scenario = DemoCatalog.All.Single(s => s.Id == id);
        var record = await BuildService().RunAsync(scenario.Request);
        Assert.Equal(expected, record.View.Status);
    }

    [Fact]
    public async Task Ready_case_leads_with_evidence_and_a_send_control()
    {
        var record = await BuildService().RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-ready").Request);
        var view = record.View;

        Assert.All(view.Criteria, c => Assert.Equal("clinical note", c.Source));
        Assert.All(view.Criteria, c => Assert.False(string.IsNullOrWhiteSpace(c.EvidenceStatement)));
        Assert.Equal(ConfidenceLevel.High, view.Confidence);
        Assert.Contains(view.Controls, c => c is { Action: "approve-send", Primary: true });
        Assert.NotNull(view.DraftBody);
    }

    [Fact]
    public async Task Appeal_case_shows_the_winning_precedents_that_drove_it()
    {
        var record = await BuildService().RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-appeal").Request);
        var view = record.View;

        Assert.NotEmpty(view.Precedents);
        Assert.Contains(view.Precedents, p => p is { WonOnAppeal: true, DroveRecommendation: true });
        Assert.True(view.Precedents[0].Similarity > 0);
        Assert.NotNull(view.AppealDraft);
    }

    [Fact]
    public async Task Mandatory_gap_surfaces_as_a_conflict_and_an_unmet_criterion()
    {
        var record = await BuildService().RunAsync(
            DemoCatalog.All.Single(s => s.Id == "demo-review-mandatory").Request);
        var view = record.View;

        Assert.Equal(CaseStatus.NeedsHumanReview, view.Status);
        Assert.NotNull(view.Gate);
        Assert.Contains("c1", view.Gate!.UnmetMandatory);
        Assert.Contains(view.Criteria, c => c is { Id: "c1", Mandatory: true, Status: CriterionStatus.Missing });
    }

    [Fact]
    public async Task Abstain_case_reports_low_confidence_and_no_send_as_primary()
    {
        var record = await BuildService().RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-abstain").Request);
        var view = record.View;

        Assert.Equal(ConfidenceLevel.Low, view.Confidence);
        Assert.DoesNotContain(view.Controls, c => c is { Action: "approve-send", Primary: true });
    }

    [Fact]
    public async Task Decision_is_recorded_against_the_case()
    {
        var service = BuildService();
        await service.RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-ready").Request);

        var updated = await service.RecordDecisionAsync("demo-ready", "approve-send", "reviewer@zynara", "looks good");

        Assert.NotNull(updated);
        Assert.Equal("approve-send", updated!.Decision!.Action);
        Assert.Equal("reviewer@zynara", updated.Decision.By);
    }
}
