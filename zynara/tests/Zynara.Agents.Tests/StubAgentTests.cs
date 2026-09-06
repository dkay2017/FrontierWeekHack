using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Agents.Stubs;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Tests;

public class StubAgentTests
{
    private static Criteria SampleCriteria() => new(
        "Bupa/Comprehensive", "MRI lumbar spine", "CCSD-MRI-LS-v3", "v3",
        new Criterion[]
        {
            new("c1", "conservative treatment such as physiotherapy tried for at least six weeks"),
            new("c2", "neurological deficit or red-flag symptoms documented"),
            new("c3", "imaging would change clinical management"),
        });

    private static CriterionStatus StatusOf(EvidenceGapAssessment a, string id) =>
        a.Findings.First(f => f.CriterionId == id).Status;

    private static PrecedentMatch Match(string id, string factPattern, AppealOutcome outcome, params string[] codes) =>
        new(new Precedent(id, "Bupa/Comprehensive", "MRI lumbar spine", Region.UK, factPattern,
                InitiallyApproved: false, AppealOutcome: outcome, DenialReasonCodes: codes,
                ClausesCited: new[] { "1.2" }, DecidedOn: new DateOnly(2026, 3, 1)),
            0.5, Array.Empty<string>());

    [Fact]
    public void Di_wires_the_five_stub_agents_by_default()
    {
        var sp = new ServiceCollection().AddZynaraAgents().BuildServiceProvider();

        Assert.IsType<StubNeedsAuthAgent>(sp.GetRequiredService<INeedsAuthAgent>());
        Assert.IsType<StubEvidenceGapAgent>(sp.GetRequiredService<IEvidenceGapAgent>());
        Assert.IsType<StubAppealBuilderAgent>(sp.GetRequiredService<IAppealBuilderAgent>());
        Assert.IsType<StubExpiryWatchAgent>(sp.GetRequiredService<IExpiryWatchAgent>());
        Assert.IsType<StubPolicyDriftAgent>(sp.GetRequiredService<IPolicyDriftAgent>());
    }

    [Fact]
    public async Task NeedsAuth_flags_disagreement_between_matching_rules()
    {
        var req = new Request
        {
            Id = "r1", Procedure = "MRI lumbar spine", PayerPlan = "Bupa/Comprehensive",
            Region = Region.UK, ClinicalNote = "n/a",
        };
        var rules = new PolicyRule[]
        {
            new("Bupa/Comprehensive", Region.UK, "MRI lumbar spine", true, "CCSD-72148", "policy-A"),
            new("Bupa/Comprehensive", Region.UK, "MRI lumbar spine", false, "", "policy-B"),
        };

        var text = await new StubNeedsAuthAgent().ExplainAsync(req, rules);

        Assert.Contains("Ambiguous", text);
        Assert.Contains("policy-A", text);
        Assert.Contains("policy-B", text);
    }

    [Fact]
    public async Task EvidenceGap_marks_a_criterion_missing_when_the_note_omits_it()
    {
        var criteria = SampleCriteria();
        var note = "Patient completed twelve weeks of physiotherapy. Imaging would change management.";

        var a = await new StubEvidenceGapAgent().AssessAsync(note, criteria);

        Assert.Equal(CriterionStatus.Documented, StatusOf(a, "c1"));
        Assert.Equal(CriterionStatus.Documented, StatusOf(a, "c3"));
        Assert.Equal(CriterionStatus.Missing, StatusOf(a, "c2"));
        a.Validate(criteria);
    }

    [Fact]
    public async Task EvidenceGap_flags_a_contradiction_on_a_negated_criterion()
    {
        var criteria = SampleCriteria();
        var note = "No neurological deficit. Physiotherapy tried for eight weeks. Imaging would change management.";

        var a = await new StubEvidenceGapAgent().AssessAsync(note, criteria);

        Assert.Equal(CriterionStatus.Contradicted, StatusOf(a, "c2"));
        Assert.True(a.AnyContradiction);
    }

    [Fact]
    public async Task EvidenceGap_grades_quality_low_when_most_criteria_are_missing()
    {
        var a = await new StubEvidenceGapAgent().AssessAsync("Patient seen.", SampleCriteria());
        Assert.Equal(EvidenceQuality.Low, a.Quality);
    }

    [Fact]
    public async Task AppealBuilder_recommends_submit_when_there_are_no_gaps()
    {
        var req = new Request
        {
            Id = "r1", Procedure = "MRI lumbar spine", PayerPlan = "Bupa/Comprehensive",
            Region = Region.UK, ClinicalNote = "n/a",
        };
        var gap = new EvidenceGapAssessment(
            new[]
            {
                new CriterionFinding("c1", CriterionStatus.Documented, null),
                new CriterionFinding("c2", CriterionStatus.Documented, null),
                new CriterionFinding("c3", CriterionStatus.Documented, null),
            }, EvidenceQuality.High, "ok");

        var rec = await new StubAppealBuilderAgent().RecommendAsync(req, gap, Array.Empty<PrecedentMatch>());

        Assert.Equal(AppealVerdict.Submit, rec.Verdict);
        Assert.Null(rec.AppealDraft);
    }

    [Fact]
    public async Task AppealBuilder_drafts_an_appeal_citing_precedents_that_won()
    {
        var req = new Request
        {
            Id = "r1", Procedure = "MRI lumbar spine", PayerPlan = "Bupa/Comprehensive",
            Region = Region.UK, ClinicalNote = "n/a", DenialLetter = "Denied: conservative treatment not documented.",
        };
        var gap = new EvidenceGapAssessment(
            new[]
            {
                new CriterionFinding("c1", CriterionStatus.Documented, null),
                new CriterionFinding("c2", CriterionStatus.Documented, null),
                new CriterionFinding("c3", CriterionStatus.Missing, null),
            }, EvidenceQuality.Medium, "one gap");
        var shortlist = new[]
        {
            Match("P-101", "6 weeks physio, radiculopathy", AppealOutcome.AppealWon, "MN-01"),
            Match("P-102", "similar", AppealOutcome.AppealWon),
        };

        var rec = await new StubAppealBuilderAgent().RecommendAsync(req, gap, shortlist);

        Assert.Equal(AppealVerdict.Appeal, rec.Verdict);
        Assert.NotNull(rec.AppealDraft);
        Assert.Contains("P-101", rec.CitedPrecedentIds);
        Assert.Contains("P-101", rec.AppealDraft!);
    }

    [Fact]
    public async Task ExpiryWatch_calls_out_a_lapse_before_the_procedure()
    {
        var auth = new AuthRecord("A-1", "r1", "Bupa/Comprehensive", "MRI lumbar spine",
            new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 1));

        var text = await new StubExpiryWatchAgent().NarrateAsync(auth, new DateOnly(2026, 6, 15), -14);

        Assert.Contains("BEFORE the procedure", text);
        Assert.Contains("Re-submission required", text);
    }

    [Fact]
    public async Task PolicyDrift_names_the_new_requirement_and_the_affected_templates()
    {
        var text = await new StubPolicyDriftAgent().ExplainImpactAsync(
            "CCSD-MRI-LS-v4",
            addedCriteria: new[] { "MRI within 12 months of symptom onset" },
            removedCriteria: Array.Empty<string>(),
            affectedTemplates: new[] { "bupa-mri-ls" });

        Assert.Contains("new requirement", text);
        Assert.Contains("bupa-mri-ls", text);
    }
}
