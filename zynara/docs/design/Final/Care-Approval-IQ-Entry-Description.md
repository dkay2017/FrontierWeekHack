# Care Approval IQ — entry description

Microsoft Agent-a-thon 2026 · Architect Track · Healthcare · Built on Microsoft Foundry and the Microsoft Agent Framework

## The issue

When an insurer denies a hospital's request for a scan, most of those denials could be fixed. The doctor's note is missing one detail, or the paperwork does not match the insurer's rules. Fixing one means a person reading long clinical notes and payer rulebooks and writing an appeal, about 20 minutes a case by our estimate. So most fixable denials are never appealed, and money that should have been paid is lost.

## Who it is for

Hospital authorisation and appeals teams. The agent prepares each case; a human reviewer always makes the final decision.

## What the agent does

Care Approval IQ prepares the appeal and refuses to send anything unsafe.

- **Five reasoning agents**, each hosted in Microsoft Foundry on GPT-5.4, each with one job: does this scan need authorisation (*needs-auth*), which insurer rules the note does not evidence (*evidence-gap*), what the note actually says and where it contradicts itself (*claims-extraction*), which arguments won in similar past cases and a draft appeal (*precedent-strategist*), and an adversarial *critic* that tries to disprove the case.
- **A deterministic referee (the Gate).** A Microsoft Agent Framework workflow, with no model in the control path, runs the agents and picks one of four routes: ready to send, needs more evidence, needs a person, or abstain.
- **A human always approves.** Every send pauses at a human-in-the-loop RequestPort. Approval levels are enforced, and sending goes through a separate identity that holds the payer credential in Key Vault. The reasoning host cannot reach it.
- **Grounded and traceable.** Each rule is checked against the note, quoted, and traced to its source. Foundry File Search supplies criteria and precedents from our own corpus. One trace covers the whole run.

## Impact

- **Measured, not claimed.** We tested 24 labelled cases against a single generalist GPT-5.4 pass given the same information. The five-agent pipeline agreed with the correct route in 100% of cases with zero unsafe automatic sends; the generalist agreed in 62.5% with four unsafe sends. It held back 4 of 4 uncertain cases, against 1 of 4. The evaluation runs as a CI gate.
- **Recoverable value.** In the demo sample, about £7,164 is recoverable from denials nobody appealed, worked out scan by scan with the working shown.
- **Time.** A case is prepared in about a minute instead of about 20 minutes by hand (the by-hand figure is our estimate).

## How it was built

- **Microsoft Foundry:** the five agents, GPT-5.4, and File Search over the precedent and criteria corpus.
- **Microsoft Agent Framework:** the workflow graph, the Gate, and the human-in-the-loop RequestPort, hosted on Azure Durable Functions.
- **Azure:** Cosmos DB for cases, Blob Storage for documents, Key Vault and managed identity for the payer credential, App Insights for traces, Static Web Apps for the reviewer dashboard.
- **Refined by:** an external review of the design, a credible generalist baseline to test against, a migration of the orchestration onto the Agent Framework, and a simplified dashboard for first-time viewers.

## Lessons learned

1. **Let AI reason and let code decide.** Keeping every decision value in deterministic code made the system testable and safe to explain.
2. **A measured baseline beats an assertion.** Testing against a fair single-agent baseline is what showed the multi-agent design was worth its cost.
3. **Separate the credential from the reasoning.** Sending through its own identity meant "the AI cannot send" is enforced by architecture, not by a prompt.
4. **Adversarial review earns its place.** The critic is the agent that most often stops a confident but weak recommendation.
5. **Know the limits.** Two routing cases still need prompt tuning, and cost management is a production requirement we did not build for this submission.

## Supporting material

Technical design document (TDD), architecture diagram, the live reviewer dashboard, and the source repository.

_To add before submitting:_ dashboard screenshots (Cases with an open case, How it thinks, Why it matters), and the example interaction: `demo-appeal` from Cases → Approve & send → Submitted.
