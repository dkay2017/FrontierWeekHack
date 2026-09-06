using Zynara.Core.Model;

namespace Zynara.Core.Tests;

/// <summary>Shared sample data — one payer, one procedure, UK.</summary>
internal static class Sample
{
    public const string Plan = "Bupa/Comprehensive";
    public const string Procedure = "MRI lumbar spine";

    public static Criteria Criteria() => new(
        Plan, Procedure, "CCSD-MRI-LS-v3", "v3",
        new Criterion[]
        {
            new("c1", "conservative treatment physiotherapy tried for at least six weeks"),
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
}
