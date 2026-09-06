# Care Approval IQ — build status / session context

**Purpose:** rehydrate context fast after a Codespace or session restart. Keep
current at every checkpoint and **commit + push** — uncommitted work is lost on a
Codespace rebuild.

_Last updated: 2026-09-06 (session 6 — P0 + slice 4 + P1-1/P1-2 dashboard +
P2-5 demo script). Deadline: **2026-09-23 midnight US**.
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

## The 4 reasoning agents + 2 monitors

**Agents** (each behind a `Zynara.Core` port + deterministic stub twin):
needs-auth · evidence-gap · appeal-builder (drafts the appeal — the
differentiator) · **critic** (tries to disprove the assembled case — D5).
**Deterministic monitors** (not agents — D6): expiry-watch (date math),
policy-drift (text diff). Payer-agnostic; **UK ⇄ US region switch** in the UI.

## Architecture — locked (session 2, 2026-09-06)

The design moved on from the original kickoff sketch. Current shape (SVG is
source of truth):

- **No queue** — the Durable Functions HTTP starter (`POST /api/requests`) is the
  async boundary (TDD §12 · TD-3). No separate `Zynara.Intake` project.
- **No SQL** — **Azure Cosmos DB** (serverless) for operational state (TD-4).
- **Blob Storage + Foundry File Search** for the unstructured corpus — policy
  docs, denial PDFs, precedent narratives (TD-5).
- **Pipeline spokes** = Durable **Activity Functions** (NeedsAuth / EvidenceGap /
  AppealMatch / Critic / Draft), each wrapping one spoke (TD-2). ExpiryMath +
  PolicyDiff run as advisory monitors, outside the pipeline (D6).
- Deterministic **Durable orchestrator owns the Gate** (TD-1) — built, session 5.
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

## Done — session 5 (2026-09-06)

- **Slice 4 — Durable orchestrator** (`src/Zynara.Orchestrator`, .NET 8 isolated
  Functions):
  - `Program.cs` — `AddZynaraAgents` + `AddZynaraCore` + an in-memory
    `IZynaraStore` seeded by `DemoWorld` (Cosmos comes with `Zynara.Data`).
  - `Http/RequestEndpoints.cs` — `POST /api/requests` (the async boundary, keyed
    on request id, idempotent) + `GET /api/requests/{id}` status probe.
  - `Orchestration/AuthOrchestrator.cs` — the deterministic hub: sequences the
    spoke activities with a 3-attempt retry, **owns the Gate** (computed inline),
    early-stops when auth is not required. Mirrors `AuthPipeline` step for step.
  - `Orchestration/SpokeActivities.cs` — one Activity Function per spoke
    (NeedsAuth / EvidenceGap / AppealMatch / Critic / Draft), each a thin adapter
    over the matching `Zynara.Core.Pipeline` class (TD-2).
  - `tests/Zynara.Orchestrator.Tests` — `FakeOrchestrationContext` + 3 tests
    (all spokes in order, early-stop, appeal never auto-submits). **57 tests
    green total.**
  - `DraftBuilder` made public so `DraftActivity` can call it.
- Running locally needs Azurite (`AzureWebJobsStorage=UseDevelopmentStorage=true`)
  and `func start`; `ZYNARA_AGENTS=stub` by default.

## Housekeeping — session 5 tail (2026-09-06)

- **`DECISIONS.md` D7–D9 added** — eval-as-CI-gate, slice-4 orchestrator, and all
  `EVALUATOR-REVIEW-RESPONSE.md` §D decisions locked.
- **`JUDGING-SELF-ASSESSMENT.md` created** — re-score vs. the evaluator's
  10-dimension card. The two flagged weaknesses (multi-agent justification,
  evaluation) moved 6.5 → ~8.0–8.5. New floor: **UX / demo (8.0)**.
- **Scope target locked: P0 + all P1 + P2** (D9 / review §D-5).

## Done — session 6 (2026-09-06) — P1-1 + P1-2

- **Reviewer read model** (`Zynara.Core/View`):
  - `CaseView` — the evidence-first projection of a `PipelineResult` (WHY, AI
    action, per-criterion evidence + source + policy clause + version, top-3
    precedent panel with similarity / won / denied / matched-facts /
    "drove the recommendation", Critic verdict + flags, conflicts, the Gate +
    its decision model, the draft, the human-control set).
  - `CaseViewBuilder` — pure, unit-tested. `CaseService` — run → project → store.
  - `ICaseRepository` + `InMemoryCaseRepository` (Cosmos later); `CaseRecord` /
    `CaseSummary` / `ReviewerDecision`.
  - `Zynara.Core/Demo` — `DemoWorld` (now + a 2nd procedure with no precedents)
    and `DemoCatalog` (5 scenarios, one per route). Moved here from the
    orchestrator; both apps share it.
- **`src/Zynara.ApiProxy`** (.NET 8 isolated Functions) — `GET /api/cases`,
  `GET /api/cases/{id}`, `POST /api/cases`, `POST /api/cases/{id}/decision`,
  `GET /api/demo/scenarios`, `POST /api/demo/scenarios/{id}/run`, `GET /api/health`.
  Self-contained for the demo (seeds + pre-runs the scenarios). CORS `*` locally.
- **`src/Zynara.Dashboard`** — one static `index.html` (no build step). Offline
  from `demo-cases.json`, or live with `?api=<url>`. Review card (evidence &
  precedent-first) + Review Queue / Early Warnings / Cost tabs +
  `staticwebapp.config.json`.
- **`tools/Zynara.DemoDump`** — regenerates `demo-cases.json` from `Zynara.Core.Demo`.
- `Zynara.Core.Tests` +7 (`CaseViewTests` — the 5 demo scenarios pinned to their
  routes, plus card-shape assertions). **67 tests green.**
- Fixed the pre-existing `StubEvidenceGapAgent` CS8604 warnings.
- **Not run through a Functions host** — no `func`/Azurite in the Codespace;
  projection logic is covered by `CaseService`/`CaseViewBuilder` tests, HTTP layer
  is thin. Same posture as the orchestrator.

## Done — session 6 tail (2026-09-06) — P2-5

- **`docs/runbooks/demo-script.md`** — 3-minute beat sheet on the review's golden
  path, WOW = the Critic overturning a plausible-looking appeal.
- **6th demo scenario `demo-appeal-critic`** — winning precedents + appeal draft,
  but only 1/3 criteria evidenced → Critic `Concerns` → HumanReview.
- **`CaseViewBuilder.Controls`** now weighs the Critic verdict + evidence quality:
  Critic uneasy / Low evidence → *"Request more evidence"* is primary, *"Approve
  & send"* de-emphasised. Headline leads with the Critic's concern. (D11)
- `Zynara.Core.Tests` +2. **69 tests green.**

## Next — remaining P1 / P2 (resume point)

> Full re-read of the COMPLETE evaluator review (text + 4 flowcharts) logged in
> `docs/review/EVALUATOR-REVIEW-RESPONSE.md` — 7 open notes; #3 (policy-citation +
> appeal-verdict eval metrics) is the main one still open (#1 done via
> `demo-appeal-critic`).

1. **"Estimated Recoverable Value"** — `Zynara.Core` model (denied count ·
   not-appealed · comparable win rate · avg recoverable value · estimate ·
   confidence · the formula) + a dashboard tile + pipeline instrumentation for a
   before/after table. (P2-1, P2-2)
2. **`config/profiles/`** — "Policy + Regulatory Profile" model + control rename
   (keep it low-key — guardrail §18). (P1-5)
3. **`Zynara.Core` audit model + `Zynara.ApiProxy` Entra roles + Cosmos
   `caseAudit`** — RBAC, approval authority tiered by risk, audit trail incl.
   agent name + version. (P1-3, + review notes #4/#5)
4. **`infra/` Bicep** — enforce the reasoning-agent ↔ payer-connectivity identity
   boundary. (P1-4)
5. **P2-3** — claims extraction + intra-evidence contradiction check + reviewer
   surface (review note #2).
6. **Eval** — add policy-citation accuracy + appeal-verdict agreement; grow the
   dataset 8 → ~20 (review note #3).

## Pending (infra / data — interleave as needed)

- `infra/` skeleton — `main.bicep` + modules, `azure.yaml`; CI (build + test +
  eval gate); enforce agent↔payer identity isolation (P1-4).
- `Zynara.Data` — Cosmos-backed `IZynaraStore` (replaces the in-memory seed in
  `Zynara.Orchestrator/Program.cs`); the real `IAgentCallRecorder`.
- Data plan — grow the labelled case set 8 → ~20 (`eval/Zynara.Eval/cases/`);
  2–3 real payer policy files for File Search.
- P2-3 — dedicated contradiction-detection flow + reviewer UI (the dimension is
  already in the decision model; this is the flow + surface).

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
