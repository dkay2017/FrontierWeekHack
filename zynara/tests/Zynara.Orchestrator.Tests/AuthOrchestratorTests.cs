using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Gating;
using Zynara.Core.Model;
using Zynara.Orchestrator.Orchestration;

namespace Zynara.Orchestrator.Tests;

/// <summary>
/// The orchestrator is a thin mirror of <c>AuthPipeline</c> (fully tested in
/// Zynara.Core.Tests). These tests pin the wiring the mirror adds: activity names,
/// the input DTOs, the early-stop, and that the Gate runs in the hub.
/// </summary>
public class AuthOrchestratorTests
{
    private static (AuthOrchestrator orch, SpokeActivities spokes, InMemoryZynaraStore store) Build(
        Action<InMemoryZynaraStore>? seed = null)
    {
        var store = new InMemoryZynaraStore();
        seed?.Invoke(store);

        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(store)
            .AddZynaraAgents()
            .AddZynaraCore()
            .AddSingleton<SpokeActivities>()
            .BuildServiceProvider();

        return (new AuthOrchestrator(sp.GetRequiredService<Gate>()),
                sp.GetRequiredService<SpokeActivities>(),
                store);
    }

    /// <summary>Routes an activity name to the matching spoke method, unwrapping the input DTO.</summary>
    private static Func<string, object?, Task<object?>> Dispatcher(SpokeActivities s) => async (name, input) => name switch
    {
        nameof(SpokeActivities.NeedsAuthActivity) => await s.NeedsAuthActivity((Request)input!),
        nameof(SpokeActivities.EvidenceGapActivity) => await s.EvidenceGapActivity((Request)input!),
        nameof(SpokeActivities.AppealMatchActivity) => await s.AppealMatchActivity((AppealMatchInput)input!),
        nameof(SpokeActivities.CriticActivity) => await s.CriticActivity((CriticInput)input!),
        nameof(SpokeActivities.DraftActivity) => await s.DraftActivity((DraftInput)input!),
        _ => throw new InvalidOperationException($"unknown activity '{name}'"),
    };

    private static void SeedMriWorld(InMemoryZynaraStore store)
    {
        store.AddRule(new PolicyRule("Bupa/Comprehensive", Region.UK, "MRI lumbar spine",
            true, "CCSD-72148", "bupa-mri-ls-v3"));
        store.AddCriteria(new Criteria("Bupa/Comprehensive", "MRI lumbar spine", "bupa-mri-ls-v3", "v3", new Criterion[]
        {
            new("c1", "physiotherapy completed for six weeks", Mandatory: true),
            new("c2", "radiculopathy documented on examination"),
            new("c3", "imaging changes management"),
        }));
    }

    private static Request Sample(string note, decimal? value = 350) => new()
    {
        Id = "req-1",
        Procedure = "MRI lumbar spine",
        PayerPlan = "Bupa/Comprehensive",
        Region = Region.UK,
        ClinicalNote = note,
        EstimatedValue = value,
    };

    [Fact]
    public async Task Runs_every_spoke_in_order_and_gates_a_complete_case()
    {
        var (orch, spokes, _) = Build(SeedMriWorld);
        var request = Sample(
            "Physiotherapy completed for eight weeks. Radiculopathy documented on examination. " +
            "Imaging changes management and alters the planned surgery.");

        var ctx = new FakeOrchestrationContext(request, Dispatcher(spokes));
        var result = await orch.Run(ctx);

        Assert.Equal(new[]
        {
            nameof(SpokeActivities.NeedsAuthActivity),
            nameof(SpokeActivities.EvidenceGapActivity),
            nameof(SpokeActivities.AppealMatchActivity),
            nameof(SpokeActivities.CriticActivity),
            nameof(SpokeActivities.DraftActivity),
        }, ctx.Calls);

        Assert.False(result.StoppedEarly);
        Assert.NotNull(result.Gate);
        Assert.Equal(GateRoute.AutoSubmit, result.Gate!.Route);
        Assert.NotNull(result.Draft);
    }

    [Fact]
    public async Task Stops_after_needs_auth_when_no_authorisation_is_required()
    {
        var (orch, spokes, _) = Build(store =>
            store.AddRule(new PolicyRule("Bupa/Comprehensive", Region.UK, "MRI lumbar spine",
                AuthRequired: false, "n/a", "bupa-mri-ls-v3")));

        var ctx = new FakeOrchestrationContext(Sample("n/a"), Dispatcher(spokes));
        var result = await orch.Run(ctx);

        Assert.Equal(new[] { nameof(SpokeActivities.NeedsAuthActivity) }, ctx.Calls);
        Assert.True(result.StoppedEarly);
        Assert.Null(result.Gate);
    }

    [Fact]
    public async Task An_appeal_is_never_auto_submitted()
    {
        var (orch, spokes, _) = Build(SeedMriWorld);
        var request = Sample(
            "Physiotherapy completed for eight weeks. Radiculopathy documented on examination. " +
            "Imaging changes management and alters the planned surgery.") with
        {
            DenialLetter = "Denied under code MN-01: conservative treatment not documented.",
        };

        var ctx = new FakeOrchestrationContext(request, Dispatcher(spokes));
        var result = await orch.Run(ctx);

        Assert.Equal(GateRoute.HumanReview, result.Gate!.Route);
    }
}
