using Zynara.Agents.Stubs;
using Zynara.Core.Agents;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Core.Tests;

public class CriticTests
{
    private static CriticContext Context(
        AppealVerdict verdict,
        EvidenceQuality quality = EvidenceQuality.High,
        bool contradiction = false,
        string[]? unmetMandatory = null,
        PrecedentMatch[]? precedents = null,
        string[]? citedPrecedents = null,
        string? draft = null)
    {
        var findings = new List<CriterionFinding>
        {
            new("c1", contradiction ? CriterionStatus.Contradicted : CriterionStatus.Documented, null),
        };
        return new CriticContext(
            Sample.Request("note", denial: verdict == AppealVerdict.Appeal ? "Denied under MN-01." : null),
            "needs auth",
            new EvidenceGapAssessment(findings, quality, "summary"),
            unmetMandatory ?? Array.Empty<string>(),
            precedents ?? Array.Empty<PrecedentMatch>(),
            new AppealRecommendation(verdict, citedPrecedents ?? Array.Empty<string>(), draft, "rec"));
    }

    private static PrecedentMatch Won(double similarity) =>
        new(Sample.Precedent("P-w", "x", AppealOutcome.AppealWon), similarity, Array.Empty<string>());

    [Fact]
    public async Task Clear_when_nothing_is_wrong()
    {
        var r = await new StubCriticAgent().ReviewAsync(Context(AppealVerdict.Submit));
        Assert.Equal(CriticVerdict.Clear, r.Verdict);
        Assert.Empty(r.Flags);
    }

    [Fact]
    public async Task Blocks_when_a_mandatory_criterion_is_unmet()
    {
        var r = await new StubCriticAgent().ReviewAsync(Context(AppealVerdict.Submit, unmetMandatory: new[] { "c1" }));
        Assert.Equal(CriticVerdict.Block, r.Verdict);
        Assert.Contains(r.Flags, f => f.Check == "mandatory-criteria");
    }

    [Fact]
    public async Task Blocks_an_appeal_recommended_without_a_winning_precedent()
    {
        var r = await new StubCriticAgent().ReviewAsync(
            Context(AppealVerdict.Appeal, precedents: new[]
            {
                new PrecedentMatch(Sample.Precedent("P-l", "x", AppealOutcome.AppealLost), 0.5, Array.Empty<string>()),
            }));
        Assert.Equal(CriticVerdict.Block, r.Verdict);
        Assert.Contains(r.Flags, f => f.Check == "recommendation-strength");
    }

    [Fact]
    public async Task Abstains_on_low_evidence_and_no_comparable_precedent()
    {
        var r = await new StubCriticAgent().ReviewAsync(
            Context(AppealVerdict.Strengthen, quality: EvidenceQuality.Low));
        Assert.Equal(CriticVerdict.Abstain, r.Verdict);
    }

    [Fact]
    public async Task Concerns_when_an_appeal_draft_cites_no_precedent()
    {
        var r = await new StubCriticAgent().ReviewAsync(
            Context(AppealVerdict.Appeal, precedents: new[] { Won(0.5) }, citedPrecedents: Array.Empty<string>(),
                draft: "We appeal this denial."));
        Assert.Equal(CriticVerdict.Concerns, r.Verdict);
        Assert.Contains(r.Flags, f => f.Check == "claim-support");
    }

    // ----- Gate ↔ Critic ------------------------------------------------------

    [Fact]
    public void Gate_abstains_when_the_critic_abstains()
    {
        var critic = new CriticReview(CriticVerdict.Abstain,
            new[] { new CriticFlag("abstention", "not enough evidence") }, "abstain");

        var d = new Gate().Evaluate(Build.Gap(quality: EvidenceQuality.High), Build.Appeal(PrecedentSupport.Strong), 1m, critic);

        Assert.Equal(GateRoute.Abstain, d.Route);
    }

    [Fact]
    public void Gate_sends_to_human_when_the_critic_blocks_even_with_perfect_numbers()
    {
        var critic = new CriticReview(CriticVerdict.Block,
            new[] { new CriticFlag("recommendation-strength", "appeal has no winning precedent") }, "block");

        var d = new Gate().Evaluate(Build.Gap(quality: EvidenceQuality.High), Build.Appeal(PrecedentSupport.Strong), 1m, critic);

        Assert.Equal(GateRoute.HumanReview, d.Route);
        Assert.Contains("Critic", d.Reason);
    }

    [Fact]
    public void Gate_downgrades_an_auto_submit_to_strengthen_on_critic_concerns()
    {
        var critic = new CriticReview(CriticVerdict.Concerns,
            new[] { new CriticFlag("claim-support", "draft cites no precedent") }, "concerns");

        var d = new Gate().Evaluate(Build.Gap(quality: EvidenceQuality.High), Build.Appeal(PrecedentSupport.Strong), 1m, critic);

        Assert.Equal(GateRoute.Strengthen, d.Route);
    }
}
