using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents.Foundry;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Tests;

public class FoundryResponseTests
{
    private static Criteria Criteria() => new(
        "Bupa/Comprehensive", "MRI lumbar spine", "CCSD-MRI-LS-v3", "v3",
        new Criterion[] { new("c1", "physiotherapy"), new("c2", "radiculopathy"), new("c3", "management change") });

    private static CriterionStatus StatusOf(EvidenceGapAssessment a, string id) =>
        a.Findings.First(f => f.CriterionId == id).Status;

    private static PrecedentMatch[] Shortlist() =>
    [
        new(new Precedent("P-1", "Bupa/Comprehensive", "MRI", Region.UK, "physio radiculopathy",
                false, AppealOutcome.AppealWon, new[] { "MN-01" }, new[] { "2.1" }, new DateOnly(2026, 3, 1)),
            0.5, Array.Empty<string>()),
    ];

    // ----- evidence-gap parsing ------------------------------------------------

    [Fact]
    public void ParseGap_maps_a_clean_json_reply()
    {
        var reply = """
            Here is the assessment:
            {"documented":["c1"],"missing":["c2"],"contradicted":["c3"],"quality":"medium","summary":"one gap"}
            """;

        var a = FoundryResponse.ParseGap(reply, Criteria());

        Assert.Equal(CriterionStatus.Documented, StatusOf(a, "c1"));
        Assert.Equal(CriterionStatus.Missing, StatusOf(a, "c2"));
        Assert.Equal(CriterionStatus.Contradicted, StatusOf(a, "c3"));
        Assert.Equal(EvidenceQuality.Medium, a.Quality);
        a.Validate(Criteria());
    }

    [Fact]
    public void ParseGap_drops_unknown_ids_and_treats_unclassified_criteria_as_missing()
    {
        var reply = """{"documented":["c1","c9"],"missing":[],"contradicted":[]}""";

        var a = FoundryResponse.ParseGap(reply, Criteria());

        Assert.Equal(CriterionStatus.Documented, StatusOf(a, "c1"));
        Assert.Equal(CriterionStatus.Missing, StatusOf(a, "c2"));   // unclassified → missing
        Assert.Equal(CriterionStatus.Missing, StatusOf(a, "c3"));
        Assert.DoesNotContain(a.Findings, f => f.CriterionId == "c9");
        a.Validate(Criteria());
    }

    [Fact]
    public void ParseGap_falls_back_to_all_missing_when_the_reply_is_not_json()
    {
        var a = FoundryResponse.ParseGap("the model rambled without any JSON", Criteria());

        Assert.All(a.Findings, f => Assert.Equal(CriterionStatus.Missing, f.Status));
        Assert.Equal(EvidenceQuality.Low, a.Quality);
        Assert.Contains("human review", a.Summary);
    }

    [Fact]
    public void ParseGap_classifies_a_criterion_at_most_once_when_the_reply_double_counts()
    {
        var reply = """{"documented":["c1"],"missing":["c1"],"contradicted":["c1"]}""";

        var a = FoundryResponse.ParseGap(reply, Criteria());

        a.Validate(Criteria());
        Assert.Equal(CriterionStatus.Documented, StatusOf(a, "c1"));   // first classification wins
    }

    // ----- appeal-builder parsing -------------------------------------------------

    [Fact]
    public void ParseAppeal_keeps_a_draft_only_for_an_appeal_verdict_after_a_denial()
    {
        var reply = """{"verdict":"appeal","cited":["P-1"],"draft":"We appeal...","rationale":"won before"}""";

        var r = FoundryResponse.ParseAppeal(reply, Shortlist(), denied: true);

        Assert.Equal(AppealVerdict.Appeal, r.Verdict);
        Assert.Equal("We appeal...", r.AppealDraft);
        Assert.Equal(new[] { "P-1" }, r.CitedPrecedentIds);
    }

    [Fact]
    public void ParseAppeal_downgrades_appeal_to_strengthen_when_there_is_no_denial()
    {
        var r = FoundryResponse.ParseAppeal("""{"verdict":"appeal","cited":[],"draft":"x"}""", Shortlist(), denied: false);

        Assert.Equal(AppealVerdict.Strengthen, r.Verdict);
        Assert.Null(r.AppealDraft);
    }

    [Fact]
    public void ParseAppeal_drops_a_cited_id_that_is_not_in_the_shortlist()
    {
        var r = FoundryResponse.ParseAppeal("""{"verdict":"submit","cited":["P-1","P-999"]}""", Shortlist(), denied: false);
        Assert.Equal(new[] { "P-1" }, r.CitedPrecedentIds);
    }

    // ----- DI wiring ------------------------------------------------------------

    [Fact]
    public void Foundry_mode_wires_the_hosted_agent_implementations()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ZYNARA_AGENTS"] = "foundry",
                ["PROJECT_ENDPOINT"] = "https://example.services.ai.azure.com/api/projects/demo",
                ["MODEL_DEPLOYMENT_NAME"] = "gpt-5.4",
            })
            .Build();

        var sp = new ServiceCollection().AddZynaraAgents(config).BuildServiceProvider();

        Assert.IsType<FoundryNeedsAuthAgent>(sp.GetRequiredService<INeedsAuthAgent>());
        Assert.IsType<FoundryEvidenceGapAgent>(sp.GetRequiredService<IEvidenceGapAgent>());
        Assert.IsType<FoundryClaimExtractionAgent>(sp.GetRequiredService<IClaimExtractionAgent>());
        Assert.IsType<FoundryAppealBuilderAgent>(sp.GetRequiredService<IAppealBuilderAgent>());
        Assert.IsType<FoundryCriticAgent>(sp.GetRequiredService<ICriticAgent>());
        Assert.Equal(7, sp.GetRequiredService<FoundryAgentProvisioner>().Specs.Count);
    }

    [Fact]
    public void Foundry_mode_without_an_endpoint_throws_a_clear_error()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ZYNARA_AGENTS"] = "foundry" })
            .Build();

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddZynaraAgents(config));
        Assert.Contains("PROJECT_ENDPOINT", ex.Message);
    }
}
