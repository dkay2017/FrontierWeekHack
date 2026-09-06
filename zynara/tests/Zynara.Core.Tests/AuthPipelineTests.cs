using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core.Abstractions;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Core.Tests;

public class AuthPipelineTests
{
    private static AuthPipeline Build(InMemoryZynaraStore store)
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
        .AddPrecedent(Sample.Precedent("P-1", "physiotherapy six weeks radiculopathy", AppealOutcome.AppealWon, "MN-01"));

    [Fact]
    public async Task Not_required_stops_before_the_gap_check()
    {
        var store = new InMemoryZynaraStore().AddRule(Sample.RuleNotRequired());

        var result = await Build(store).RunAsync(Sample.Request("n/a"));

        Assert.True(result.StoppedEarly);
        Assert.Null(result.Gap);
        Assert.Null(result.Draft);
    }

    [Fact]
    public async Task A_complete_note_auto_submits_and_produces_a_cited_draft()
    {
        var store = Seeded();
        var note = "Conservative physiotherapy for eight weeks. Radiculopathy documented. " +
                   "MRI result would change management.";

        var result = await Build(store).RunAsync(Sample.Request(note, value: 350m));

        Assert.False(result.StoppedEarly);
        Assert.NotNull(result.Gate);
        Assert.True(result.Gate!.AutoSubmit, result.Gate.Reason);
        Assert.NotNull(result.Draft);
        Assert.NotEmpty(result.Draft!.CitedCriteria);
        Assert.Equal("CCSD-72148", result.Draft.RequiredCode);
    }

    [Fact]
    public async Task A_note_with_a_gap_routes_to_review()
    {
        var store = Seeded();
        var note = "Physiotherapy tried. MRI would change management."; // c2 (radiculopathy) missing

        var result = await Build(store).RunAsync(Sample.Request(note));

        Assert.False(result.Gate!.AutoSubmit);
        Assert.Contains("review", result.Gate.Reason);
        Assert.NotNull(result.Draft);        // a draft is still assembled for the reviewer
    }

    [Fact]
    public async Task A_high_value_request_routes_to_review_even_with_a_clean_note()
    {
        var store = Seeded();
        var note = "Conservative physiotherapy for eight weeks. Radiculopathy documented. " +
                   "MRI result would change management.";

        var result = await Build(store).RunAsync(Sample.Request(note, value: 9000m));

        Assert.False(result.Gate!.AutoSubmit);
        Assert.Contains("auto-limit", result.Gate.Reason);
    }
}
