using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Impact;
using Zynara.Core.View;

namespace Zynara.Core.Tests;

/// <summary>Before / after benchmark (P2-2 / evaluator §16) — the pipeline column is measured, not estimated.</summary>
public class BenchmarkTests
{
    private static ServiceProvider Sp() => new ServiceCollection()
        .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
        .AddZynaraAgents()
        .AddZynaraCore()
        .BuildServiceProvider();

    [Fact]
    public async Task Pipeline_run_records_measured_metrics()
    {
        var sp = Sp();
        var record = await sp.GetRequiredService<CaseService>()
            .RunAsync(DemoCatalog.All.Single(s => s.Id == "demo-appeal").Request);

        var m = record.Result.Metrics;
        Assert.Equal(3, m.CriteriaChecked);
        Assert.Equal(4, m.ReasoningStepsRun);
        Assert.True(m.PrecedentsConsidered > 0);
        Assert.True(m.AssembledInMs >= 0);
    }

    [Fact]
    public async Task Benchmark_aggregates_over_the_assembled_cases_and_labels_the_estimates()
    {
        var sp = Sp();
        var cases = sp.GetRequiredService<CaseService>();
        foreach (var s in DemoCatalog.All) await cases.RunAsync(s.Request);

        var report = await sp.GetRequiredService<BenchmarkService>().GetAsync();

        Assert.True(report.Measured.Cases >= 5);
        Assert.True(report.Measured.AvgCriteriaChecked > 0);
        Assert.All(report.Rows, r => Assert.False(string.IsNullOrWhiteSpace(r.Basis)));
        Assert.Contains(report.Rows, r => r.ManualIsEstimate);
        Assert.Contains("No improvement percentage is claimed", report.Disclaimer);
        Assert.Equal(0d, report.Measured.UnsafeAutomationRate);
    }
}
