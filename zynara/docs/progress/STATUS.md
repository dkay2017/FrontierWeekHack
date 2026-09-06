# Care Approval IQ — build status / session context

**Purpose:** rehydrate context fast after a Codespace or session restart. Keep
current at every checkpoint and **commit + push** — uncommitted work is lost on a
Codespace rebuild.

_Last updated: 2026-09-06 (session 2). Deadline: **2026-09-23 midnight US**.
Submission: 3-min video (required) + repo + architecture doc + TDD + dashboard UI._

## What this is

**Zynara Health — Care Approval IQ.** "Proof beats paperwork." A 5-agent prior
authorisation & appeals system for the Microsoft Agent-a-thon 2026 (Architect
track, EMEA region). **C# / .NET 8**, Azure AI Foundry Agent Service, Azure
Durable Functions. Full design: `docs/design/ARCHITECTURE.md` + `Care-Approval-IQ-TDD.md`.
**The architecture SVG/PNG in `docs/design/` is the authoritative picture** —
docs must agree with it.

Separate from the team's prior project (TireForge/Meridian — untouched, kept as
proof-of-concept). Lives in `zynara/` alongside `tireforge/` in the **same
GitHub repo** (`dkay2017/FrontierWeekHack`); **new Azure resource group**;
patterns reused, not code.

## The 5 agents

needs-auth · evidence-gap · appeal-builder (drafts the appeal — the
differentiator) · expiry-watch · policy-drift. Each behind a `Zynara.Core`
interface with a deterministic stub twin. Payer-agnostic; **UK ⇄ US region
switch** in the UI.

## Architecture — locked (session 2, 2026-09-06)

The design moved on from the original kickoff sketch. Current shape (SVG is
source of truth):

- **No queue** — the Durable Functions HTTP starter (`POST /api/requests`) is the
  async boundary (TDD §12 · TD-3). No separate `Zynara.Intake` project.
- **No SQL** — **Azure Cosmos DB** (serverless) for operational state (TD-4).
- **Blob Storage + Foundry File Search** for the unstructured corpus — policy
  docs, denial PDFs, precedent narratives (TD-5).
- **5 deterministic spokes** = Durable **Activity Functions** (NeedsAuthCheck /
  EvidenceGapMatch / AppealMatch / ExpiryMath / PolicyDiff), each wrapping one
  agent call (TD-2).
- Deterministic **Durable orchestrator owns the Gate** (TD-1).
- Dashboard = Static Web App, 3 tabs: Review Queue (+ Recovery £ stat) · Early
  Warnings · Cost. UK⇄US is a header control.
- Cost metering **is in scope**; the AI-governance enforcement layer, private
  networking, CI/CD deploy pipeline, HA/DR etc. are **named in TDD §7.1**, not
  built.

## Done — session 1 (2026-09-05)

- Branding: Zynara Health / Care Approval IQ / "Proof beats paperwork" / logo.
- `zynara/` folder + structure, `README.md`, `.gitignore`, `global.json`.
- `ARCHITECTURE.md` + `Care-Approval-IQ-TDD.md` written.

## Done — session 2 (2026-09-06)

- **Architecture SVG — rev 6.** Many review rounds. Final: distinct section
  colours (INTAKE teal, COMPUTE blue, FOUNDRY violet, DATA green, EXPERIENCE
  teal-cyan, CROSS-CUTTING pale yellow-green), white hub card, bigger blue title,
  cross-cutting header icons, inline subtitles, readable fonts, tight margins.
  `Care-Approval-IQ-Architecture_Design.png` = 2× render, embedded in TDD §4 for
  Word/PDF export.
- **TDD brought fully current** — Cosmos/Blob/File Search, no queue/SQL/Intake,
  §12 TD-1…TD-6 (added TD-6 platform choices), §7.1 rewritten as a full
  Production-Readiness table (private VNet, AI-governance layer, **CI/CD
  pipeline**, HA/DR, secrets, PHI-at-scale, pen test, real payer connectivity).
- **Agent layer — slice 1 committed.** `Zynara.sln` + `Zynara.Core`,
  `Zynara.Agents`, `Zynara.Core.Tests`, `Zynara.Agents.Tests`.
  - `Zynara.Core/Model/Domain.cs` — Request, PolicyRule, Criteria/Criterion,
    Precedent, AuthRecord, PolicyVersion, Region/AppealOutcome.
  - `Zynara.Core/Agents/Contracts.cs` — the 5 ports + typed outputs.
  - `Zynara.Core/Agents/IAgentCallRecorder.cs` — cost-meter port + Null impl.
  - `Zynara.Agents/Stubs/*` — deterministic twin per agent.
  - `Zynara.Agents/DependencyInjection.cs` — `AddZynaraAgents()`;
    `ZYNARA_AGENTS=foundry` reserved for slice 3.
  - 11 tests green (`dotnet test Zynara.sln`).

## Done — session 3 (2026-09-06)

- **Slice 2 — deterministic spokes + Gate** (`Zynara.Core`): `IZynaraStore` +
  `InMemoryZynaraStore`, `Model/Results.cs`, the 5 spokes (NeedsAuthCheck,
  EvidenceGapMatch + readiness, AppealMatch, ExpiryMath, PolicyDiff), `Gate`,
  `AuthPipeline`, `AddZynaraCore()`. 20 tests.
- **Slice 3 — Foundry agents** (`Zynara.Agents/Foundry`): `FoundryAgentClient`,
  `FoundryAgentOptions`, `AgentPrompts`, `FoundryAgentProvisioner`,
  `Foundry{NeedsAuth,EvidenceGap,AppealBuilder,ExpiryWatch,PolicyDrift}Agent`,
  `FoundryResponse` (testable JSON sanitisers), `ZYNARA_AGENTS=foundry` DI.
  17 agent tests. **37 tests green total.**

## PIVOT — session 4: independent evaluator review

Full review + plan: **`docs/review/EVALUATOR-REVIEW-RESPONSE.md`**. Overall
~8.0/10; weak spots = multi-agent justification (6.5) and evaluation evidence
(6.5). Working the **P0** items before resuming the orchestrator (slice 4),
because P0-2/P0-3 rework the Gate + evidence model that slice 2 just built.

**Decisions taken:**
- **D-2 — expiry-watch + policy-drift demoted** to deterministic services with an
  optional thin AI narrator; they are no longer counted as reasoning "agents"
  (the review flagged the flaw — date math / text diff should not need an agent).
- D-3 — P0 first, before slice 4.

**P0 work order:** P0-3 (structured decision model — reworks Gate/evidence) →
P0-2 (Critic agent) → P0-1 (reframe docs) → P0-4 (eval suite). **All P0 done
(2026-09-06).**

- **P0-3** — structured decision model, mandatory criteria never averaged, the
  `Abstain` route (D4). Core tests green.
- **P0-2** — the Critic agent (7 checks, verdict feeds the Gate — D5). Stub +
  Foundry twins. Agent tests green.
- **P0-1** — TDD §3.1 / §7.3 / §11.1, ARCHITECTURE §5/§6/§8/§10 + mermaids,
  `DECISIONS.md` (D1–D6), SVG + PNG now show 4 agents + Critic + the 4-route Gate.
- **P0-4** — `eval/Zynara.Eval`: metric suite + generalist baseline + 8 labelled
  cases; `dotnet test` hard-gates unsafe-automation = 0 and mandatory-FN = 0.
  Latest: route agreement 100%, unsafe automation 0 (baseline 2), safe
  abstention 1/1, 0 hallucinated references.

Next: resume **slice 4 — the Durable orchestrator** (see Pending #1).

## Pending (deferred until P0 is in)

1. **Slice 4 — orchestrator** (`Zynara.Orchestrator` Durable Functions):
   HTTP starter + orchestrator + activity functions (NeedsAuthCheck /
   EvidenceGapMatch / AppealMatch / Critic / Gate; ExpiryMath + PolicyDiff as
   advisory timers).
2. `infra/` skeleton — `main.bicep` + module stubs, `azure.yaml`; CI (build +
   test + eval gate); enforce agent↔payer identity isolation (P1-4).
3. Data plan — grow the labelled clinical-case set beyond the initial 8
   (`eval/Zynara.Eval/cases/`); 2–3 real payer policy files for File Search.
4. `Zynara.Dashboard` — evidence-first HITL workspace (P1-1) + precedent panel
   (P1-2).
5. `Zynara.ApiProxy` — read models + reviewer actions + RBAC (P1-3).
6. `config/profiles/` — "Policy + Regulatory Profile" (P1-5).
7. `DECISIONS.md` — start the delta log (record the P0 changes as D1…Dn).

## Timeline (18 days)

| Dates | Phase |
|---|---|
| Sep 5–7 | Setup — brand ✅, repo ✅, arch doc ✅, TDD ✅, agent-layer slice 1 ✅ · then infra skeleton, CI, Challenge 0 provision |
| Sep 7–10 | Data + de-risk spike (one Foundry agent end-to-end) |
| Sep 10–16 | Core build — deterministic spokes, 5 agents, orchestrator, gate, region switch, Cosmos, API (Challenge 1) |
| Sep 16–19 | Experience — dashboard, demo flow, Recovery view, the wow moment |
| Sep 18–20 | Challenges 2–4 — traces, eval, portal workflow |
| Sep 20–22 | Harden + rehearse — deploy, live E2E verify, record video, write pitch + 5-point doc, self re-score |
| Sep 22–23 | Buffer + submit early |

**Demo-ready target: Sep 20.** Submit: Sep 22.

## Reused patterns (from TireForge — learnings, not code)

Hybrid deterministic+agent split · Gate + human approval · citation grounding ·
managed-identity-first infra + Key Vault refs · cost metering · Durable Functions
shape · agent port + stub-twin + Foundry-impl behind a `.Core` interface · CI +
tests from commit 1 · strict self-scored judging assessment before submit.

## Judging criteria (never lose sight)

Innovation · Usability · Impact — 30 each, 90 total. EMEA region, ~6 winners.
Judged by Microsoft-recognised MVPs. See `ARCHITECTURE.md` §15 / TDD §11.
