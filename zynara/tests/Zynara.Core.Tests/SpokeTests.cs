using Zynara.Agents.Stubs;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Core.Tests;

public class SpokeTests
{
    [Fact]
    public async Task NeedsAuthCheck_stops_the_pipeline_when_auth_is_not_required()
    {
        var store = new InMemoryZynaraStore().AddRule(Sample.RuleNotRequired());
        var na = await new NeedsAuthCheck(store, new StubNeedsAuthAgent())
            .RunAsync(Sample.Request("n/a"));

        Assert.False(na.AuthRequired);
        Assert.False(na.Ambiguous);
    }

    [Fact]
    public async Task NeedsAuthCheck_defaults_to_required_and_calls_the_agent_when_rules_disagree()
    {
        var store = new InMemoryZynaraStore()
            .AddRule(Sample.RuleRequired())
            .AddRule(Sample.RuleNotRequired());

        var na = await new NeedsAuthCheck(store, new StubNeedsAuthAgent())
            .RunAsync(Sample.Request("n/a"));

        Assert.True(na.AuthRequired);
        Assert.True(na.Ambiguous);
        Assert.Contains("Ambiguous", na.Note);
    }

    [Fact]
    public async Task EvidenceGapMatch_derives_readiness_as_met_over_total()
    {
        var store = new InMemoryZynaraStore().AddCriteria(Sample.Criteria());
        // note mentions physiotherapy (c1) and management (c3), not radiculopathy (c2)
        var note = "Twelve weeks of physiotherapy completed. MRI result would change management.";

        var gap = await new EvidenceGapMatch(store, new StubEvidenceGapAgent())
            .RunAsync(Sample.Request(note));

        Assert.Equal(2.0 / 3.0, gap.Readiness, 3);
        Assert.Contains("c2", gap.Assessment.Missing);
    }

    [Fact]
    public async Task AppealMatch_ranks_a_precedent_that_shares_the_denial_reason_code_to_the_top()
    {
        var store = new InMemoryZynaraStore()
            .AddPrecedent(Sample.Precedent("P-far", "unrelated shoulder arthroscopy case", AppealOutcome.AppealLost))
            .AddPrecedent(Sample.Precedent("P-near", "physiotherapy six weeks radiculopathy lumbar",
                AppealOutcome.AppealWon, "MN-01"));

        var req = Sample.Request(
            "physiotherapy for six weeks, radiculopathy present",
            denial: "Denied under MN-01: conservative treatment not documented.");
        var emptyGap = new EvidenceGapAssessment(
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), "");

        var result = await new AppealMatch(store, new StubAppealBuilderAgent())
            .RunAsync(req, emptyGap);

        Assert.Equal("P-near", result.Shortlist[0].CaseId);
        Assert.Equal(AppealVerdict.Appeal, result.Recommendation.Verdict);
    }

    [Fact]
    public async Task ExpiryMath_raises_a_warning_when_the_margin_is_thin()
    {
        var store = new InMemoryZynaraStore().AddAuth(new AuthRecord(
            "A-1", "req-1", Sample.Plan, Sample.Procedure,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 10)));

        var result = await new ExpiryMath(store, new StubExpiryWatchAgent())
            .RunAsync("req-1", procedureDate: new DateOnly(2026, 6, 20));

        Assert.Equal(-10, result.DaysOfMargin);
        Assert.NotNull(result.Warning);
        Assert.Single(store.EarlyWarnings);
    }

    [Fact]
    public async Task ExpiryMath_is_silent_when_there_is_plenty_of_margin()
    {
        var store = new InMemoryZynaraStore().AddAuth(new AuthRecord(
            "A-1", "req-1", Sample.Plan, Sample.Procedure,
            new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));

        var result = await new ExpiryMath(store, new StubExpiryWatchAgent())
            .RunAsync("req-1", procedureDate: new DateOnly(2026, 6, 1));

        Assert.Null(result.Warning);
        Assert.Empty(store.EarlyWarnings);
    }

    [Fact]
    public async Task PolicyDiff_detects_a_new_criterion_between_the_two_latest_versions()
    {
        var store = new InMemoryZynaraStore()
            .AddPolicyVersion(new PolicyVersion("CCSD-MRI-LS", "v3", new DateOnly(2026, 1, 1),
                new Criterion[] { new("a", "physiotherapy for six weeks") }))
            .AddPolicyVersion(new PolicyVersion("CCSD-MRI-LS", "v4", new DateOnly(2026, 5, 1),
                new Criterion[]
                {
                    new("a", "physiotherapy for six weeks"),
                    new("b", "MRI within 12 months of symptom onset"),
                }));

        var result = await new PolicyDiff(store, new StubPolicyDriftAgent())
            .RunAsync("CCSD-MRI-LS", affectedTemplates: new[] { "bupa-mri-ls" });

        Assert.Contains("MRI within 12 months of symptom onset", result.AddedCriteria);
        Assert.NotNull(result.Warning);
    }
}
