using Zynara.Core.Model;

namespace Zynara.Core.Demo;

/// <summary>One ready-made request the dashboard can run, with a label for the picker.</summary>
public sealed record DemoScenario(string Id, string Label, string Expectation, Request Request);

/// <summary>
/// The demo scenarios — five requests against <see cref="DemoWorld"/> that exercise
/// each Gate route. The clinical notes are written so the deterministic stub agents
/// produce the intended findings; the hosted agents should reach the same routes.
/// </summary>
public static class DemoCatalog
{
    public static IReadOnlyList<DemoScenario> All { get; } = new[]
    {
        new DemoScenario(
            "demo-ready",
            "Complete record — ready to submit",
            "All criteria documented, high-quality evidence → auto-submit on approval.",
            new Request
            {
                Id = "demo-ready",
                Procedure = DemoWorld.Procedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Physiotherapy completed over eight weeks with limited benefit. Radiculopathy " +
                    "documented on examination in an L5 distribution with reduced reflexes. MRI " +
                    "imaging changes management — the findings would determine whether to proceed to " +
                    "decompression surgery.",
            }),

        new DemoScenario(
            "demo-strengthen",
            "One gap — needs strengthening",
            "Mandatory met, a supporting criterion undocumented → back to the clinician.",
            new Request
            {
                Id = "demo-strengthen",
                Procedure = DemoWorld.Procedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Physiotherapy completed for six weeks. MRI requested; imaging changes management " +
                    "and would alter the planned surgical approach.",
            }),

        new DemoScenario(
            "demo-review-mandatory",
            "Mandatory criterion missing — human review",
            "No physiotherapy evidenced → the Critic blocks and a reviewer must decide.",
            new Request
            {
                Id = "demo-review-mandatory",
                Procedure = DemoWorld.Procedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Radiculopathy documented on examination. Imaging changes management and would " +
                    "determine the surgical approach.",
            }),

        new DemoScenario(
            "demo-contradiction",
            "The note contradicts itself — human resolves it",
            "One sentence says physiotherapy was completed, another that the patient could not attend it. " +
            "Claims-extraction flags the conflict; the Gate does not pick a side — it routes to a human.",
            new Request
            {
                Id = "demo-contradiction",
                Procedure = DemoWorld.Procedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Imaging changes management and would alter the surgical plan. Physiotherapy " +
                    "completed for eight weeks with good adherence. On later review the patient reports " +
                    "they were unable to attend physiotherapy.",
            }),

        new DemoScenario(
            "demo-appeal",
            "Denied — appeal with winning precedents",
            "Denial on record; two comparable cases won on appeal → precedent-backed appeal, human-approved.",
            new Request
            {
                Id = "demo-appeal",
                Procedure = DemoWorld.Procedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Physiotherapy completed for eight weeks. Radiculopathy documented on examination. " +
                    "Imaging changes management and determines the surgical approach.",
                DenialLetter =
                    "Prior authorisation denied under code MN-01: conservative treatment not evidenced.",
            }),

        new DemoScenario(
            "demo-appeal-critic",
            "Denied — the Critic challenges an over-reaching appeal",
            "Comparable cases won, but only one of three criteria is evidenced → the Critic flags that " +
            "the appeal recommendation is firmer than the record supports; the reviewer is steered to " +
            "request evidence, not file.",
            new Request
            {
                Id = "demo-appeal-critic",
                Procedure = DemoWorld.Procedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Physiotherapy completed for six weeks. MRI of the lumbar spine requested.",
                DenialLetter =
                    "Prior authorisation denied under code MN-01: conservative treatment not evidenced.",
            }),

        new DemoScenario(
            "demo-abstain",
            "Thin record, no precedent — the system abstains",
            "Rarer procedure with no comparable history and low-quality evidence → the system declines to advise.",
            new Request
            {
                Id = "demo-abstain",
                Procedure = DemoWorld.RareProcedure,
                PayerPlan = DemoWorld.Payer,
                Region = Region.UK,
                EstimatedValue = 350m,
                ClinicalNote =
                    "Physiotherapy attempted. Ongoing shoulder pain, query labral pathology. " +
                    "Requesting MR arthrogram to clarify.",
            }),
    };
}
