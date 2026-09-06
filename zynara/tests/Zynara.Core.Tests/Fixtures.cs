using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Tests;

/// <summary>Shared sample data — one payer, one procedure, UK.</summary>
internal static class Sample
{
    public const string Plan = "Bupa/Comprehensive";
    public const string Procedure = "MRI lumbar spine";

    /// <summary>c1 mandatory (conservative treatment), c2 + c3 supporting.</summary>
    public static Criteria Criteria() => new(
        Plan, Procedure, "CCSD-MRI-LS-v3", "v3",
        new Criterion[]
        {
            new("c1", "conservative treatment physiotherapy tried for at least six weeks", Mandatory: true),
            new("c2", "neurological deficit or radiculopathy documented"),
            new("c3", "imaging result would change clinical management"),
        });

    public static PolicyRule RuleRequired() =>
        new(Plan, Region.UK, Procedure, AuthRequired: true, RequiredCode: "CCSD-72148", PolicyRef: "bupa-mri-ls-A");

    public static PolicyRule RuleNotRequired() =>
        new(Plan, Region.UK, Procedure, AuthRequired: false, RequiredCode: "", PolicyRef: "bupa-mri-ls-B");

    public static Request Request(string note, string? denial = null, decimal? value = null) => new()
    {
        Id = "req-1",
        Procedure = Procedure,
        PayerPlan = Plan,
        Region = Region.UK,
        ClinicalNote = note,
        DenialLetter = denial,
        EstimatedValue = value,
    };

    public static Precedent Precedent(
        string id, string factPattern, AppealOutcome outcome, params string[] reasonCodes) =>
        new(id, Plan, Procedure, Region.UK, factPattern,
            InitiallyApproved: outcome == AppealOutcome.NotAppealed,
            AppealOutcome: outcome,
            DenialReasonCodes: reasonCodes,
            ClausesCited: new[] { "criterion 2.1" },
            DecidedOn: new DateOnly(2026, 3, 1));

    public static PrecedentMatch Match(Precedent p, double similarity = 0.4) =>
        new(p, similarity, Array.Empty<string>());
}

/// <summary>Terse builders for the structured evidence / gate types.</summary>
internal static class Build
{
    public static EvidenceGapAssessment Assessment(
        EvidenceQuality quality = EvidenceQuality.High, params (string id, CriterionStatus status)[] findings) =>
        new(findings.Select(f => new CriterionFinding(f.id, f.status, null)).ToList(), quality, "test");

    public static EvidenceGapResult Gap(
        bool mandatoryPass = true,
        int mandatoryTotal = 1,
        int documented = 8, int partial = 0, int missing = 0,
        bool contradiction = false,
        EvidenceQuality quality = EvidenceQuality.High,
        params string[] unmetMandatory) =>
        new(Assessment(quality), mandatoryPass, mandatoryTotal, documented, partial, missing, contradiction)
        {
            UnmetMandatory = unmetMandatory,
        };

    public static AppealMatchResult Appeal(PrecedentSupport support = PrecedentSupport.Moderate) =>
        new(Array.Empty<PrecedentMatch>(),
            new StrategyRecommendation(StrategyVerdict.Submit, Array.Empty<string>(), null, "ok"),
            support);
}
