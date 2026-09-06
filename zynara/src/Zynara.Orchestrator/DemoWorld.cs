using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Orchestrator;

/// <summary>
/// Seeds the in-memory store for local runs and the demo (slice 4 has no Cosmos
/// yet). One payer world — Bupa / Comprehensive, MRI lumbar spine, UK — with the
/// written criteria and a handful of precedents, matching the labelled eval cases.
/// </summary>
internal static class DemoWorld
{
    private const string Payer = "Bupa/Comprehensive";
    private const string Procedure = "MRI lumbar spine";
    private const string PolicyRef = "bupa-mri-ls-v3";

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

        store.AddPrecedent(new Precedent(
            "P-1", Payer, Procedure, Region.UK,
            FactPattern: "physiotherapy six weeks radiculopathy imaging management lumbar spine",
            InitiallyApproved: false, AppealOutcome: AppealOutcome.AppealWon,
            DenialReasonCodes: new[] { "MN-01" }, ClausesCited: new[] { "2.1" },
            DecidedOn: new DateOnly(2026, 3, 1)));

        store.AddPrecedent(new Precedent(
            "P-2", Payer, Procedure, Region.UK,
            FactPattern: "physiotherapy radiculopathy imaging lumbar spine management",
            InitiallyApproved: false, AppealOutcome: AppealOutcome.AppealWon,
            DenialReasonCodes: new[] { "MN-01" }, ClausesCited: new[] { "2.1" },
            DecidedOn: new DateOnly(2026, 4, 1)));

        store.AddPrecedent(new Precedent(
            "P-9", Payer, Procedure, Region.UK,
            FactPattern: "physiotherapy radiculopathy imaging lumbar spine",
            InitiallyApproved: false, AppealOutcome: AppealOutcome.AppealLost,
            DenialReasonCodes: new[] { "MN-01" }, ClausesCited: Array.Empty<string>(),
            DecidedOn: new DateOnly(2026, 2, 1)));

        return store;
    }
}
