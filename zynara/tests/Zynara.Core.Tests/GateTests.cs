using Zynara.Core.Agents;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Core.Tests;

public class GateTests
{
    [Fact]
    public void Ready_to_submit_when_mandatory_met_no_contradiction_high_evidence_and_within_value()
    {
        var d = new Gate().Evaluate(
            Build.Gap(documented: 3, missing: 0, quality: EvidenceQuality.High),
            Build.Appeal(PrecedentSupport.Strong),
            estimatedValue: 400m);

        Assert.Equal(GateRoute.ReadyToSubmit, d.Route);
    }

    [Fact]
    public void A_missing_mandatory_criterion_routes_to_human_review_regardless_of_supporting_evidence()
    {
        var d = new Gate().Evaluate(
            Build.Gap(mandatoryPass: false, documented: 20, quality: EvidenceQuality.High, unmetMandatory: new[] { "c1" }),
            Build.Appeal(PrecedentSupport.Strong),
            estimatedValue: 1m);

        Assert.Equal(GateRoute.HumanReview, d.Route);
        Assert.Contains("mandatory criterion unmet (c1)", d.Reason);
    }

    [Fact]
    public void A_contradiction_routes_to_human_review_and_is_not_silently_resolved()
    {
        var d = new Gate().Evaluate(
            Build.Gap(contradiction: true, quality: EvidenceQuality.High),
            Build.Appeal(PrecedentSupport.Strong),
            estimatedValue: 1m);

        Assert.Equal(GateRoute.HumanReview, d.Route);
        Assert.Contains("contradictory", d.Reason);
    }

    [Fact]
    public void Low_evidence_and_weak_precedent_support_makes_the_system_abstain()
    {
        var d = new Gate().Evaluate(
            Build.Gap(quality: EvidenceQuality.Low, missing: 2),
            Build.Appeal(PrecedentSupport.None),
            estimatedValue: 1m);

        Assert.Equal(GateRoute.Abstain, d.Route);
        Assert.Contains("abstains", d.Reason);
    }

    [Fact]
    public void Over_the_auto_value_limit_routes_to_human_review()
    {
        var d = new Gate(new GateOptions { AutoLimit = 500m }).Evaluate(
            Build.Gap(quality: EvidenceQuality.High),
            Build.Appeal(PrecedentSupport.Strong),
            estimatedValue: 9000m);

        Assert.Equal(GateRoute.HumanReview, d.Route);
        Assert.Contains("auto-limit", d.Reason);
    }

    [Fact]
    public void An_undocumented_supporting_criterion_asks_the_clinician_to_strengthen()
    {
        var d = new Gate().Evaluate(
            Build.Gap(documented: 2, missing: 1, quality: EvidenceQuality.High),
            Build.Appeal(PrecedentSupport.Moderate),
            estimatedValue: 1m);

        Assert.Equal(GateRoute.Strengthen, d.Route);
    }

    [Fact]
    public void The_decision_always_carries_its_working()
    {
        var d = new Gate().Evaluate(Build.Gap(), Build.Appeal(), 1m);
        Assert.Contains("mandatory", d.Reason);
        Assert.Contains("precedent support", d.Reason);
        Assert.NotNull(d.Model);
    }
}
