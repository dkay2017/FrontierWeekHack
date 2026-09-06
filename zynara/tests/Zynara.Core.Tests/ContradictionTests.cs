using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Agents.Stubs;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Demo;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;
using Zynara.Core.View;

namespace Zynara.Core.Tests;

/// <summary>
/// The contradiction-detection flow (P2-3 / evaluator finding #4): claims →
/// pair on the same subject → a conflict is never silently resolved.
/// </summary>
public class ContradictionTests
{
    private static ContradictionCheck Check() => new(new StubClaimExtractionAgent());

    [Fact]
    public async Task A_note_that_states_and_denies_the_same_subject_yields_a_conflict()
    {
        var req = Sample.Request(
            "Physiotherapy completed for eight weeks. The patient was unable to attend physiotherapy.");

        var result = await Check().RunAsync(req);

        Assert.True(result.HasConflicts);
        Assert.Equal("physiotherapy", result.Conflicts[0].Subject);
        Assert.False(result.Conflicts[0].Affirmed.Negated);
        Assert.True(result.Conflicts[0].Denied.Negated);
    }

    [Fact]
    public async Task A_consistent_note_yields_no_conflict()
    {
        var req = Sample.Request(
            "Physiotherapy completed for eight weeks. Radiculopathy documented on examination.");

        Assert.False((await Check().RunAsync(req)).HasConflicts);
    }

    [Fact]
    public async Task The_demo_contradiction_case_routes_to_a_human_and_surfaces_the_conflict()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();

        var record = await sp.GetRequiredService<CaseService>()
            .RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-contradiction").Request);

        Assert.True(record.Result.Contradiction.HasConflicts);
        Assert.Equal(GateRoute.HumanReview, record.Result.Gate!.Route);
        Assert.Contains(record.View.Conflicts, c => c.Contains("contradicts itself"));
    }
}
