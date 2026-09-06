using Zynara.Core.Agents;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Core.Tests;

public class GateTests
{
    private static EvidenceGapResult Gap(double readiness, int missing = 0, int conflicts = 0)
    {
        var met = Enumerable.Range(0, 8).Select(i => $"m{i}").ToArray();
        var miss = Enumerable.Range(0, missing).Select(i => $"x{i}").ToArray();
        var conf = Enumerable.Range(0, conflicts).Select(i => $"k{i}").ToArray();
        return new EvidenceGapResult(new EvidenceGapAssessment(met, miss, conf, "t"), readiness);
    }

    [Fact]
    public void Auto_submits_when_ready_no_gaps_and_within_the_value_limit()
    {
        var d = new Gate().Evaluate(Gap(0.90), estimatedValue: 400m);
        Assert.True(d.AutoSubmit);
        Assert.Contains("auto-submit", d.Reason);
    }

    [Fact]
    public void Exact_threshold_passes()
    {
        var d = new Gate(new GateOptions { ReadinessThreshold = 0.80 }).Evaluate(Gap(0.80), null);
        Assert.True(d.AutoSubmit);
    }

    [Fact]
    public void Routes_to_review_below_the_readiness_threshold()
    {
        var d = new Gate().Evaluate(Gap(0.70), null);
        Assert.False(d.AutoSubmit);
        Assert.Contains("readiness 0.70 <", d.Reason);
    }

    [Fact]
    public void Routes_to_review_when_a_criterion_is_missing_even_if_readiness_is_high()
    {
        var d = new Gate().Evaluate(Gap(0.95, missing: 1), null);
        Assert.False(d.AutoSubmit);
        Assert.Contains("1 criterion(a) missing", d.Reason);
    }

    [Fact]
    public void Routes_to_review_when_a_criterion_is_contradicted()
    {
        var d = new Gate().Evaluate(Gap(0.95, conflicts: 1), null);
        Assert.False(d.AutoSubmit);
        Assert.Contains("contradiction", d.Reason);
    }

    [Fact]
    public void Routes_to_review_above_the_auto_value_limit()
    {
        var d = new Gate(new GateOptions { AutoLimit = 500m }).Evaluate(Gap(0.95), estimatedValue: 5000m);
        Assert.False(d.AutoSubmit);
        Assert.Contains("auto-limit", d.Reason);
    }
}
