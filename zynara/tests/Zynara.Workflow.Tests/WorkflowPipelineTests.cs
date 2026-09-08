using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Demo;
using Zynara.Core.Model;

namespace Zynara.Workflow.Tests;

/// <summary>
/// Phase 1 — the MAF Workflow produces the same <see cref="PipelineResult"/> the
/// v1 <c>AuthPipeline</c> did. Mirrors <c>Zynara.Core.Tests.AuthPipelineTests</c>
/// plus a full sweep of the demo catalogue. Runs entirely in-process via
/// <c>InProcessExecution</c> — no infra.
/// </summary>
public class WorkflowPipelineTests
{
    private static CareApprovalRunner Runner(InMemoryZynaraStore store) =>
        new ServiceCollection()
            .AddSingleton<IZynaraStore>(store)
            .AddZynaraAgents()
            .AddZynaraCore()
            .AddZynaraWorkflow()
            .BuildServiceProvider()
            .GetRequiredService<CareApprovalRunner>();

    private static InMemoryZynaraStore Demo() => DemoWorld.Seed(new InMemoryZynaraStore());

    private static Request Scenario(string id) => DemoCatalog.All.Single(x => x.Id == id).Request;

    [Theory]
    [InlineData("demo-ready", GateRoute.ReadyToSubmit)]
    [InlineData("demo-strengthen", GateRoute.Strengthen)]
    [InlineData("demo-review-mandatory", GateRoute.HumanReview)]
    [InlineData("demo-contradiction", GateRoute.HumanReview)]
    [InlineData("demo-appeal", GateRoute.HumanReview)]
    [InlineData("demo-appeal-critic", GateRoute.HumanReview)]
    [InlineData("demo-abstain", GateRoute.Abstain)]
    public async Task Every_demo_scenario_routes_the_same_as_v1(string scenarioId, GateRoute expected)
    {
        var result = await Runner(Demo()).RunAsync(Scenario(scenarioId));
        Assert.Equal(expected, result.Gate!.Route);
    }

    [Fact]
    public async Task Not_required_stops_before_the_gap_check()
    {
        var store = new InMemoryZynaraStore().AddRule(new PolicyRule(
            "Bupa/Comprehensive", Region.UK, "MRI lumbar spine",
            AuthRequired: false, "n/a", "bupa-mri-ls-v3"));

        var result = await Runner(store).RunAsync(new Request
        {
            Id = "nr-1", Procedure = "MRI lumbar spine", PayerPlan = "Bupa/Comprehensive",
            Region = Region.UK, ClinicalNote = "n/a",
        });

        Assert.True(result.StoppedEarly);
        Assert.Null(result.Gap);
        Assert.Null(result.Draft);
    }

    [Fact]
    public async Task A_complete_note_is_ready_to_submit_and_produces_a_cited_draft()
    {
        var result = await Runner(Demo()).RunAsync(Scenario("demo-ready"));

        Assert.False(result.StoppedEarly);
        Assert.Equal(GateRoute.ReadyToSubmit, result.Gate!.Route);
        Assert.NotNull(result.Draft);
        Assert.NotEmpty(result.Draft!.CitedCriteria);
        Assert.Equal("CCSD-72148", result.Draft.RequiredCode);
    }

    [Fact]
    public async Task An_unmet_mandatory_criterion_routes_to_human_review_and_the_critic_blocks()
    {
        var result = await Runner(Demo()).RunAsync(Scenario("demo-review-mandatory"));

        Assert.Equal(GateRoute.HumanReview, result.Gate!.Route);
        Assert.NotNull(result.Critic);
        Assert.Equal(CriticVerdict.Block, result.Critic!.Verdict);
        Assert.NotNull(result.Draft);
    }

    [Fact]
    public async Task The_accumulator_carries_every_spoke_output()
    {
        var result = await Runner(Demo()).RunAsync(Scenario("demo-appeal"));

        Assert.NotNull(result.NeedsAuth);
        Assert.NotNull(result.Gap);
        Assert.NotNull(result.Appeal);
        Assert.NotNull(result.Critic);
        Assert.NotNull(result.Gate);
        Assert.NotNull(result.Draft);
    }
}
