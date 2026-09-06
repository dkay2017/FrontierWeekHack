using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Core.Demo;

/// <summary>
/// Seeds the in-memory store for local runs and the demo (no Cosmos yet — see
/// <see cref="InMemoryZynaraStore"/>). One payer world: Bupa / Comprehensive,
/// MRI lumbar spine, UK — the written criteria and three precedents on record.
/// </summary>
public static class DemoWorld
{
    public const string Payer = "Bupa/Comprehensive";
    public const string Procedure = "MRI lumbar spine";
    public const string PolicyRef = "bupa-mri-ls-v3";

    /// <summary>A second, rarer procedure with a rule and criteria but <b>no precedent history</b>.</summary>
    public const string RareProcedure = "MR arthrogram shoulder";
    public const string RarePolicyRef = "bupa-mras-v1";

    public static InMemoryZynaraStore Seed(InMemoryZynaraStore store)
    {
        store.AddRule(new PolicyRule(
            Payer, Region.UK, Procedure,
            AuthRequired: true, RequiredCode: "CCSD-72148", PolicyRef: PolicyRef,
            Notes: "Prior authorisation required for outpatient MRI of the lumbar spine."));

        store.AddCriteria(new Criteria(Payer, Procedure, PolicyRef, "v3", new Criterion[]
        {
            new("c1", "physiotherapy completed for six weeks", Mandatory: true),
            new("c2", "radiculopathy documented on examination"),
            new("c3", "imaging changes management"),
        }));

        store.AddRule(new PolicyRule(
            Payer, Region.UK, RareProcedure,
            AuthRequired: true, RequiredCode: "CCSD-73222", PolicyRef: RarePolicyRef,
            Notes: "Prior authorisation required for MR arthrogram of the shoulder."));

        store.AddCriteria(new Criteria(Payer, RareProcedure, RarePolicyRef, "v1", new Criterion[]
        {
            new("c1", "conservative management including physiotherapy attempted", Mandatory: true),
            new("c2", "instability or labral tear suspected clinically"),
            new("c3", "plain radiographs already obtained"),
        }));

        store.AddPrecedent(new Precedent(
            "P-1", Payer, Procedure, Region.UK,
            FactPattern:
                "Patient completed eight weeks of physiotherapy with radiculopathy documented on " +
                "examination and imaging expected to change management. Initially denied under MN-01 " +
                "(conservative treatment not evidenced); authorised on appeal under clause 2.1.",
            InitiallyApproved: false, AppealOutcome: AppealOutcome.AppealWon,
            DenialReasonCodes: new[] { "MN-01" }, ClausesCited: new[] { "2.1" },
            DecidedOn: new DateOnly(2026, 3, 1)));

        store.AddPrecedent(new Precedent(
            "P-2", Payer, Procedure, Region.UK,
            FactPattern:
                "Six weeks of physiotherapy, radiculopathy on examination, MRI requested to plan " +
                "surgical management. Denied MN-01, overturned on appeal citing clause 2.1 and the " +
                "documented neurological deficit.",
            InitiallyApproved: false, AppealOutcome: AppealOutcome.AppealWon,
            DenialReasonCodes: new[] { "MN-01" }, ClausesCited: new[] { "2.1" },
            DecidedOn: new DateOnly(2026, 4, 1)));

        store.AddPrecedent(new Precedent(
            "P-9", Payer, Procedure, Region.UK,
            FactPattern:
                "Physiotherapy and radiculopathy noted but imaging not shown to change management. " +
                "Denied MN-02; appeal unsuccessful — the record did not establish that the scan would " +
                "alter the treatment plan.",
            InitiallyApproved: false, AppealOutcome: AppealOutcome.AppealLost,
            DenialReasonCodes: new[] { "MN-02" }, ClausesCited: Array.Empty<string>(),
            DecidedOn: new DateOnly(2026, 2, 1)));

        // Denial-history cohorts — the inputs to Estimated Recoverable Value (P2-1).
        // Synthetic aggregates; in production the system rolls these up from the case record.
        store.AddDenialCohort(new DenialCohort(
            Payer, Procedure, Region.UK,
            Denied: 46, Appealed: 27, AppealsWon: 19, NotAppealed: 15,
            MeanClaimValue: 540m,
            WindowStart: new DateOnly(2025, 9, 1), WindowEnd: new DateOnly(2026, 9, 1)));

        store.AddDenialCohort(new DenialCohort(
            Payer, RareProcedure, Region.UK,
            Denied: 12, Appealed: 5, AppealsWon: 2, NotAppealed: 6,
            MeanClaimValue: 610m,
            WindowStart: new DateOnly(2025, 9, 1), WindowEnd: new DateOnly(2026, 9, 1)));

        return store;
    }
}
