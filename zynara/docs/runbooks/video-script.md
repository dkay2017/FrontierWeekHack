# Submission video — script and shot list

**Audience:** Agent-a-Thon 2026 judges, Architect track. Technical, time-poor, scoring
Innovation · Usability · Impact, and looking for proof that the agents work *together*.
This is a technical walkthrough, not a client pitch: show the design, the platform and the
evidence, not benefits language.

**Length:** 3:00 target. `[CUT]` marks what to drop to reach 2:00.
**Pace:** about 150 words a minute (about 450 words in total). Record screen first, then read the voiceover once, calmly.
**One message:** *five reasoning agents prepare the case, a deterministic referee decides, a human presses send, and we measured it.*

## Shot list

| Time | SHOW (where) | VOICEOVER |
|---|---|---|
| 0:00–0:15 | **Title card** (logo, tagline "Proof beats paperwork"). | "Insurers deny scans every day, and most fixable denials are never appealed. Care Approval IQ prepares that case, and it is built so the AI cannot send anything on its own." |
| 0:15–0:45 | **TECHNICAL DIAGRAM** — `Care-Approval-IQ-Architecture_Design.png`. Slow highlight: Intake → Compute (Microsoft Agent Framework workflow) → the five Foundry agents → the Gate → RequestPort → Submission Adapter. | "The design has one rule: the AI reasons, code decides. A Microsoft Agent Framework workflow, with no model in the control path, calls five hosted Foundry agents: needs-auth, evidence-gap, claims-extraction, precedent-strategist and a Critic. A deterministic Gate picks the route. Every send pauses at a human RequestPort. Sending goes through a separate identity that holds the payer credential. The reasoning host cannot reach it." |
| 0:45–1:05 | **AI FOUNDRY PORTAL** (ai.azure.com → project `care-approval`) — the agent list showing the five agents. Open **critic**: instructions and the File Search tool. `[CUT to 5 s: agent list only]` | "These are real hosted agents in Azure AI Foundry, running GPT-5.4. The Critic has one job: try to disprove the case. Precedents and criteria come from File Search over our own corpus." |
| 1:05–1:40 | **DASHBOARD → Cases.** Open `demo-appeal`. Point at the rules list (quote + 📄 source + 📋 rule chips), then the similar past cases. | "A reviewer sees the denied request already prepared. Each rule is checked against the note, quoted, and traced to its source. Similar past cases show which arguments the insurer has accepted before." |
| 1:40–2:05 | **DASHBOARD → How it thinks** → run "Some evidence is missing". Land on Critic **Concerns** → Referee **needs more evidence** → **not sent**. | "Here the Critic pushes back: the recommendation is firmer than the evidence supports. The referee is plain code, so it holds the case. The system can refuse its own recommendation." |
| 2:05–2:30 | **DASHBOARD → Cases.** Role = Senior reviewer. `demo-appeal` → **Approve & send** → "Submitted" toast → open *Show the technical details* → audit trail. `[CUT: skip role switch, show the toast only]` | "Approval is a person's decision, with authority levels enforced. On approve, the Submission Adapter uses the Key Vault credential, and the whole run is one audited trace." |
| 2:30–2:50 | **EVIDENCE** — `eval/Zynara.Eval/BASELINE.md` slide (or the results table). Then a 3-second flash of the green CI run. | "Does multi-agent beat one generalist? We tested 24 labelled cases against a single GPT-5.4 pass. Ours: 100% route agreement, zero unsafe sends. The generalist: 62.5% and four unsafe sends. The CI gate enforces that." |
| 2:50–3:00 | **DASHBOARD → Why it matters**, £7,164, then **closing card** with repo URL. `[CUT: closing card only]` | "About £7,000 of recoverable value in this sample, with the working shown. Proof beats paperwork." |

## Where each artifact goes (checklist)

- **Technical diagram:** 0:15–0:45, full screen. This is the Architect-track centrepiece; do not shorten it below 25 s.
- **Foundry portal (agents):** 0:45–1:05, proves the agents are hosted and real. Open it before recording and have the `critic` agent tab ready.
- **Foundry / App Insights trace:** optional 5-s cutaway at 2:20 during the audit-trail line, showing the single `pipeline.run → invoke_agent → chat` trace (Challenge 2).
- **Live dashboard:** 1:05–2:30 plus 2:50, https://proud-water-0e35b1603.6.azurestaticapps.net/
- **Repo / CI:** 3 s at 2:45, green build; not a tour.

## Before recording

1. **Reseed the 7 demo cases** so the queue is fresh; approving a case changes it, so reseed again before the real take.
2. **`demo-ready` routes to "Needs more evidence" live** (known soft-routing item). Do not use it. `demo-appeal` works for the send.
3. Zoom the browser to 125%, 1080p, light theme, notifications off, one window.
4. Pre-open tabs in order: diagram · Foundry portal · dashboard · BASELINE.md · CI.
5. **Rehearse twice with a stopwatch**; cut to the `[CUT]` version if you are over.
6. Add captions (many judges watch muted).
7. Fallback: if the live send fails, cut to a clean earlier take and do not retry on camera.

## Do not

- Tour Azure services beyond the Foundry agents, or read the architecture aloud line by line.
- Say "will win": use "the payer's own history shows which arguments have succeeded".
- Claim the simulator is the live pipeline; if you show it, say "replay".
