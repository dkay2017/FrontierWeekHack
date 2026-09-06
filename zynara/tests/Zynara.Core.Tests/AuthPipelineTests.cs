using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core.Abstractions;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Core.Tests;

public class AuthPipelineTests
{
    private static AuthPipeline BuildPipeline(InMemoryZynaraStore store)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(store)
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();
        return sp.GetRequiredService<AuthPipeline>();
    }

    private static InMemoryZynaraStore Seeded() => new InMemoryZynaraStore()
        .AddRule(Sample.RuleRequired())
        .AddCriteria(Sample.Criteria())
        .AddPrecedent(Sample.Precedent("P-1", "physiotherapy six weeks radiculopathy lumbar spine",
            AppealOutcome.AppealWon, "MN-01"))
        .AddPrecedent(Sample.Precedent("P-2", "physiotherapy radiculopathy lumbar",
            AppealOutcome.AppealWon, "MN-01"));

    [Fact]
    public async Task Not_required_stops_before_the_gap_check()
    {
        var store = new InMemoryZynaraStore().AddRule(Sample.RuleNotRequired());

        var result = await BuildPipeline(store).RunAsync(Sample.Request("n/a"));

        Assert.True(result.StoppedEarly);
        Assert.Null(result.Gap);
        Assert.Null(result.Draft);
    }

    [Fact]
    public async Task A_complete_note_auto_submits_and_produces_a_cited_draft()
    {
        var note = "Conservative physiotherapy for eight weeks. Radiculopathy documented. " +
                   "MRI result would change management.";

        var result = await BuildPipeline(Seeded()).RunAsync(Sample.Request(note, value: 350m));

        Assert.False(result.StoppedEarly);
        Assert.Equal(GateRoute.AutoSubmit, result.Gate!.Route);
        Assert.NotNull(result.Draft);
        Assert.NotEmpty(result.Draft!.CitedCriteria);
        Assert.Equal("CCSD-72148", result.Draft.RequiredCode);
    }

    [Fact]
    public async Task An_unmet_mandatory_criterion_routes_to_human_review()
    {
        var note = "Radiculopathy documented. MRI would change management."; // no physiotherapy → mandatory c1 unmet

        var result = await BuildPipeline(Seeded()).RunAsync(Sample.Request(note));

        Assert.Equal(GateRoute.HumanReview, result.Gate!.Route);
        Assert.Contains("mandatory criterion unmet", result.Gate.Reason);
        Assert.NotNull(result.Draft);        // a draft is still assembled for the reviewer
    }

    [Fact]
    public async Task A_high_value_request_routes_to_review_even_with_a_clean_note()
    {
        var note = "Conservative physiotherapy for eight weeks. Radiculopathy documented. " +
                   "MRI result would change management.";

        var result = await BuildPipeline(Seeded()).RunAsync(Sample.Request(note, value: 9000m));

        Assert.Equal(GateRoute.HumanReview, result.Gate!.Route);
        Assert.Contains("auto-limit", result.Gate.Reason);
    }
}
