using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Workflow.Tests;

/// <summary>
/// PHASE 0 SPIKE — prove the MAF Workflow pattern: the pipeline runs as a graph
/// via <c>InProcessExecution</c> (no infra), driven by the deterministic stub
/// agents, and produces a <see cref="GateDecision"/> matching the v1 pipeline.
/// </summary>
public class SpikeTests
{
    private static (Microsoft.Agents.AI.Workflows.Workflow wf, IServiceScope scope) BuildWorkflow(InMemoryZynaraStore store)
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(store)
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();
        var scope = sp.CreateScope();
        var s = scope.ServiceProvider;

        var wf = CareApprovalWorkflow.Build(
            s.GetRequiredService<NeedsAuthCheck>(),
            s.GetRequiredService<EvidenceGapMatch>(),
            s.GetRequiredService<ContradictionCheck>(),
            s.GetRequiredService<AppealMatch>(),
            s.GetRequiredService<CriticCheck>(),
            s.GetRequiredService<Gate>());
        return (wf, scope);
    }

    private static async Task<PipelineState> RunAsync(Microsoft.Agents.AI.Workflows.Workflow wf, Request request)
    {
        await using var run = await InProcessExecution.RunAsync(wf, request);
        var output = run.NewEvents.OfType<WorkflowOutputEvent>().LastOrDefault()
            ?? throw new InvalidOperationException("workflow produced no output");
        return output.As<PipelineState>()!;
    }

    [Theory]
    [InlineData("demo-ready", GateRoute.AutoSubmit)]
    [InlineData("demo-review-mandatory", GateRoute.HumanReview)]
    [InlineData("demo-abstain", GateRoute.Abstain)]
    public async Task The_graph_routes_a_scenario_the_same_way_the_v1_pipeline_does(string scenarioId, GateRoute expected)
    {
        var store = DemoWorld.Seed(new InMemoryZynaraStore());
        var (wf, scope) = BuildWorkflow(store);
        using var _ = scope;

        var request = DemoCatalog.All.Single(x => x.Id == scenarioId).Request;
        var state = await RunAsync(wf, request);

        Assert.NotNull(state.Decision);
        Assert.Equal(expected, state.Decision!.Route);
    }

    [Fact]
    public async Task Every_spoke_populated_the_accumulator()
    {
        var store = DemoWorld.Seed(new InMemoryZynaraStore());
        var (wf, scope) = BuildWorkflow(store);
        using var _ = scope;

        var state = await RunAsync(wf, DemoCatalog.All.Single(x => x.Id == "demo-appeal").Request);

        Assert.NotNull(state.NeedsAuth);
        Assert.NotNull(state.Gap);
        Assert.NotNull(state.Appeal);
        Assert.NotNull(state.Critic);
        Assert.NotNull(state.Decision);
    }
}
