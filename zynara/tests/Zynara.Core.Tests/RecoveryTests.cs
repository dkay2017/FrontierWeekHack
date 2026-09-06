using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Model;
using Zynara.Core.Recovery;

namespace Zynara.Core.Tests;

/// <summary>
/// Estimated Recoverable Value (P2-1 / evaluator §14). The number must always
/// equal its stated formula, and confidence must track the sample size.
/// </summary>
public class RecoveryTests
{
    private static DenialCohort Cohort(int denied, int appealed, int won, int notAppealed, decimal mean) =>
        new("Bupa/Comprehensive", "MRI lumbar spine", Region.UK,
            denied, appealed, won, notAppealed, mean,
            new DateOnly(2025, 9, 1), new DateOnly(2026, 9, 1));

    [Fact]
    public void Estimate_is_not_appealed_times_win_rate_times_mean_value()
    {
        var e = RecoveryEstimator.Estimate(Cohort(46, 27, 19, 15, 540m));

        // 15 × (19/27) × 540 = 5700
        Assert.Equal(5700m, e.EstimatedRecoverableValue);
        Assert.Equal(19d / 27d, e.ComparableWinRate, 5);
        Assert.Contains("15 not appealed", e.Formula);
        Assert.Contains("£540", e.Formula);
    }

    [Fact]
    public void Confidence_tracks_the_sample_size()
    {
        Assert.Equal(RecoveryConfidence.High, RecoveryEstimator.Estimate(Cohort(46, 27, 19, 15, 540m)).Confidence);
        Assert.Equal(RecoveryConfidence.Medium, RecoveryEstimator.Estimate(Cohort(20, 10, 6, 6, 500m)).Confidence);
        Assert.Equal(RecoveryConfidence.Low, RecoveryEstimator.Estimate(Cohort(12, 5, 2, 6, 610m)).Confidence);
    }

    [Fact]
    public void No_appeals_on_record_means_a_zero_estimate_not_a_crash()
    {
        var e = RecoveryEstimator.Estimate(Cohort(9, 0, 0, 9, 500m));
        Assert.Equal(0m, e.EstimatedRecoverableValue);
        Assert.Equal(0d, e.ComparableWinRate);
    }

    [Fact]
    public void Portfolio_sums_the_scopes_and_blends_the_win_rate()
    {
        var cohorts = new[] { Cohort(46, 27, 19, 15, 540m), Cohort(12, 5, 2, 6, 610m) with { Procedure = "MR arthrogram shoulder" } };

        var total = RecoveryEstimator.Portfolio(cohorts);

        Assert.Equal(58, total.Denied);
        Assert.Equal(21, total.NotAppealed);
        Assert.Equal(32, total.Appealed);
        Assert.Equal(21, total.AppealsWon);
        Assert.Equal(21d / 32d, total.ComparableWinRate, 5);
        // 5700 + (6 × 0.4 × 610 = 1464) = 7164
        Assert.Equal(7164m, total.EstimatedRecoverableValue);
    }

    [Fact]
    public void Empty_history_returns_the_empty_estimate()
    {
        Assert.Same(RecoveryEstimate.Empty, RecoveryEstimator.Portfolio(Array.Empty<DenialCohort>()));
    }

    [Fact]
    public async Task Recovery_service_reads_the_demo_cohorts()
    {
        var sp = new ServiceCollection()
            .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
            .AddZynaraAgents()
            .AddZynaraCore()
            .BuildServiceProvider();

        var report = await sp.GetRequiredService<RecoveryService>().GetAsync();

        Assert.Equal(2, report.ByScope.Count);
        Assert.True(report.Total.EstimatedRecoverableValue > 0m);
        Assert.Equal(report.ByScope.Sum(s => s.EstimatedRecoverableValue), report.Total.EstimatedRecoverableValue);
        // ordered biggest opportunity first
        Assert.True(report.ByScope[0].EstimatedRecoverableValue >= report.ByScope[1].EstimatedRecoverableValue);
    }
}
