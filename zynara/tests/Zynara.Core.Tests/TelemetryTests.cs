using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Diagnostics;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Core.Tests;

/// <summary>
/// Challenge 2 — the pipeline emits an agent-keyed span tree
/// (<c>pipeline.run</c> → <c>spoke.*</c> → <c>invoke_agent *</c>).
/// </summary>
public class TelemetryTests
{
    private sealed record Span(string Name, ActivitySpanId Id, ActivitySpanId ParentId, Activity Activity);

    // Other test classes run pipelines in parallel on the same ActivitySource, so
    // every capture is scoped to the trace id of this test's own pipeline.run.
    private static (List<Span> all, ActivityListener listener) Capture()
    {
        var all = new List<Span>();
        var listener = new ActivityListener
        {
            ShouldListenTo = s => s.Name == ZynaraTelemetry.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a =>
            {
                lock (all) all.Add(new Span(a.OperationName, a.SpanId, a.ParentSpanId, a));
            },
        };
        ActivitySource.AddActivityListener(listener);
        return (all, listener);
    }

    private static List<Span> ForRequest(List<Span> all, string requestId)
    {
        lock (all)
        {
            var run = all.Single(s => s.Name == "pipeline.run"
                && (string?)s.Activity.GetTagItem("zynara.request_id") == requestId);
            return all.Where(s => s.Activity.TraceId == run.Activity.TraceId).ToList();
        }
    }

    private static Request Request(string id, string note, decimal? value = null) => new()
    {
        Id = id,
        Procedure = Sample.Procedure,
        PayerPlan = Sample.Plan,
        Region = Region.UK,
        ClinicalNote = note,
        EstimatedValue = value,
    };

    private static AuthPipeline BuildPipeline(InMemoryZynaraStore store) =>
        new ServiceCollection()
            .AddSingleton<IZynaraStore>(store)
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider()
            .GetRequiredService<AuthPipeline>();

    private static InMemoryZynaraStore Seeded() => new InMemoryZynaraStore()
        .AddRule(Sample.RuleRequired())
        .AddCriteria(Sample.Criteria())
        .AddPrecedent(Sample.Precedent("P-1", "physiotherapy six weeks radiculopathy lumbar spine",
            AppealOutcome.AppealWon, "MN-01"));

    [Fact]
    public async Task A_full_run_emits_a_nested_pipeline_run_spoke_agent_tree()
    {
        var (all, listener) = Capture();
        using var _ = listener;

        const string id = "telemetry-full";
        var note = "Conservative physiotherapy for eight weeks. Radiculopathy documented. " +
                   "MRI result would change management.";
        await BuildPipeline(Seeded()).RunAsync(Request(id, note, value: 350m));

        var spans = ForRequest(all, id);
        var run = spans.Single(s => s.Name == "pipeline.run");
        Assert.Equal("AutoSubmit", run.Activity.GetTagItem("zynara.route"));

        foreach (var spoke in new[] { "spoke.evidence-gap", "spoke.claims-extraction",
                                      "spoke.precedent-match", "spoke.critic" })
            Assert.Equal(run.Id, spans.Single(x => x.Name == spoke).ParentId);

        AssertAgentUnderSpoke(spans, "invoke_agent evidence-gap", "spoke.evidence-gap");
        AssertAgentUnderSpoke(spans, "invoke_agent claims-extraction", "spoke.claims-extraction");
        AssertAgentUnderSpoke(spans, "invoke_agent precedent-strategist", "spoke.precedent-match");
        AssertAgentUnderSpoke(spans, "invoke_agent critic", "spoke.critic");
    }

    [Fact]
    public async Task Not_required_emits_only_the_needs_auth_spoke_and_stops()
    {
        var (all, listener) = Capture();
        using var _ = listener;

        const string id = "telemetry-stop";
        var store = new InMemoryZynaraStore().AddRule(Sample.RuleNotRequired());
        await BuildPipeline(store).RunAsync(Request(id, "n/a"));

        var spans = ForRequest(all, id);
        var run = spans.Single(s => s.Name == "pipeline.run");
        Assert.Equal(true, run.Activity.GetTagItem("zynara.stopped_early"));
        Assert.Contains(spans, s => s.Name == "spoke.needs-auth");
        Assert.DoesNotContain(spans, s => s.Name == "spoke.critic");
    }

    private static void AssertAgentUnderSpoke(List<Span> spans, string agent, string spoke) =>
        Assert.Equal(spans.Single(s => s.Name == spoke).Id, spans.Single(s => s.Name == agent).ParentId);
}
