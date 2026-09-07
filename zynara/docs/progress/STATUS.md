# Care Approval IQ — build status / session context

**Purpose:** rehydrate context fast after a Codespace or session restart. Keep
current at every checkpoint and **commit + push** — uncommitted work is lost on a
Codespace rebuild.

_Last updated: 2026-09-07 (session 10 — MAF **Phase 4**: durable host built &
verified end-to-end locally, coarse-graph CustomStatus fix, infra + api-proxy
wired. Left: `azd` deploy + live verify + merge. `v1.0-durable` = rollback).
Deadline: **2026-09-23 midnight US**. Submission: 3-min video + repo + arch doc
+ TDD + dashboard UI._

## ⚑ Active work — MAF migration (`maf-migration` branch)

**Decision (D32):** rebuild the orchestration layer on the Microsoft Agent
Framework — the reference pattern for the team's forthcoming agent projects, and
Microsoft's recommended pattern. Not a deadline compromise. Rollback = tag
**`v1.0-durable`** (`git checkout v1.0-durable && azd deploy`); `main` stays there
until Phase 4 merges. Plan + findings: `docs/design/MAF-MIGRATION.md`. Decisions:
D32 (why), D33 (Phase 0–3 checkpoint).

| Phase | State |
|---|---|
| 0 · Spike | ✅ GO — pattern proven, blast radius small, tests simpler than `FakeOrchestrationContext` |
| 1 · Full graph | ✅ `CareApprovalWorkflow.Build` → `PipelineResult`; `CareApprovalRunner` drop-in for `AuthPipeline` |
| 2 · Agents via MAF | ✅ `AsAIAgent` over the existing Foundry agents; **verified against real GPT-5.4** (routes match v1) |
| 3 · HITL review port | ✅ explicit `RequestPort` pause/resume + `ApprovalAuthority` + submission edge |
| 4 · Durable hosting + deploy | ⏳ **host verified end-to-end locally** (Azurite) — both paths complete, no CustomStatus error. Infra + api-proxy wired & committed. **Left: `azd provision` + `azd deploy` (user-run), verify live chain, merge to `main`.** |
| 5 · Cleanup + docs | ⏳ delete v1 orchestrator, TDD/ARCH/SVG, `v2.0-maf` tag |

**Phase 4 detail** (`docs/design/MAF-MIGRATION.md` §"Phase 4"):
`Zynara.WorkflowHost` = `FunctionsApplication` + `ConfigureDurableWorkflows`.
`CareApprovalWorkflow.BuildDurable` is a **coarse** graph (one `assemble` executor
runs the whole pipeline + persists to Cosmos; the message downstream is the slim
`Flow` record) — required to stay under Durable Task's 16 KB CustomStatus cap.
Wired into `infra/` as the 4th Function app (`func-zynara-workflowhost-<suffix>`,
own task hub `ZynaraMafPipeline`), runs **alongside** the v1 orchestrator until
Phase 5. `WorkflowClient` in the api-proxy forwards `/run` + `/decision` to it
(`runId` = request id); dashboard contract unchanged.

**Unchanged by the migration:** `Zynara.Core` domain + Gate + `ApprovalAuthority`
+ the pipeline logic + `eval/` + `Zynara.Data` + `Zynara.Submission` + the
dashboard design + most of `infra/`.

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
needs-auth · evidence-gap · precedent-strategist (drafts the appeal — the
differentiator) · **critic** (tries to disprove the assembled case — D5).
**Deterministic monitors** (not agents — D6): expiry-watch (date math),
policy-drift (text diff). Payer-agnostic; **Policy + Regulatory Profile** (UK ⇄ US) behind the header chip.

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
- Dashboard = Static Web App, 3 tabs: Review Queue · Early Warnings ·
  Recoverable value. the Policy + Regulatory Profile sits behind the header chip.
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
  `Foundry{NeedsAuth,EvidenceGap,PrecedentStrategist}Agent`,
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

## Parked — 2026-09-06 (end of session 6)

Everything committed + pushed. Resume at **"Next — remaining P1 / P2"** below.
Dashboard: reworked into the light-first Zynara/TireForge house style (masthead +
seal logo, aurora ground, tinted cards, table + right-hand drawer for the case
detail). Published artifact:
`https://claude.ai/code/artifact/5c26a346-ad1d-41e5-8859-79bc88b9b65b`. User
approved the direction ("much better"). **S-5 (D29): the old dark `index.html` is
deleted; this light version *is* `index.html` now, with an optional `?api=` live
mode.**

## Done — session 6 tail (2026-09-06) — P2-5

- **`docs/runbooks/demo-script.md`** — 3-minute beat sheet on the review's golden
  path, WOW = the Critic overturning a plausible-looking appeal.
- **6th demo scenario `demo-appeal-critic`** — winning precedents + appeal draft,
  but only 1/3 criteria evidenced → Critic `Concerns` → HumanReview.
- **`CaseViewBuilder.Controls`** now weighs the Critic verdict + evidence quality:
  Critic uneasy / Low evidence → *"Request more evidence"* is primary, *"Approve
  & send"* de-emphasised. Headline leads with the Critic's concern. (D11)
- `Zynara.Core.Tests` +2. **69 tests green.**

## Done — 2026-09-06 — P2-1

- **Estimated Recoverable Value** (D12). `Zynara.Core/Recovery` — `RecoveryEstimator`
  (pure: `notAppealed × winRate × meanClaimValue`, always carries the formula
  string + a confidence level from sample size), `RecoveryService`, `DenialCohort`
  model, `IZynaraStore.GetDenialCohortsAsync`, `DemoWorld` seeds 2 cohorts.
  `Zynara.ApiProxy` `GET /api/recovery`. `tools/Zynara.DemoDump` includes it.
  Dashboard: renamed the Cost tab → **Recoverable value** (headline number, the
  4 inputs, the arithmetic, confidence, by-scope table). `Zynara.Core.Tests` +6.
  **75 tests green.** Artifact republished.
- Demo total: £7,164 (High confidence) — MRI-LS £5,700 + MR-arthrogram £1,464.

## Done — 2026-09-06 — P1-5

- **Policy + Regulatory Profile** (D14). `Zynara.Core/Profiles` — `RegulatoryProfile`
  (policy pack · terminology map · coding conventions · regulators · appeal path ·
  integration profile · currency), built-in `RegulatoryProfiles.Uk`/`.Us`.
  `config/profiles/*.json` = override format, generated by `DemoDump` (no drift).
  `Zynara.ApiProxy` `GET /api/profiles` + `/{region}`. Dashboard: the header chip
  → a small profile panel (kept low-key, guardrail §18). `Zynara.Core.Tests` +2.
  **77 tests green.** TDD §7.2 rewritten. Artifact republished.

## Done — 2026-09-06 — P1-3

- **RBAC + approval authority + audit trail** (D15). `Zynara.Core/Authority` —
  `ApprovalAuthority`: 4 ranked roles, required-to-approve = max(route tier,
  financial-risk tier), appeal ≥ Senior. `CaseRecord.Audit` (append-only):
  pipeline run + each step's agent **impl + version** (`IAgentRoster`) + reviewer
  actions (id · role · time · note) + refusals with reason.
  `RecordDecisionAsync` → `DecisionOutcome`; `Zynara.ApiProxy` reads
  `X-Reviewer-Role` → 403 on refusal. `CaseView` +`ApproveAuthority` +`Audit`.
  Dashboard: masthead role picker; drawer audit-trail section; "Approve & send"
  disabled ("needs Senior reviewer") below the required authority. Also fixed the
  draft submission/appeal toggle. `Zynara.Core.Tests` +9. **86 tests green.**
- Still open for P1-3: Cosmos `caseAudit` container (with `Zynara.Data`), real
  Entra app-role binding (with `infra/`).

## Done — 2026-09-06 — P2-2

- **Before / after benchmark** (D16). `AuthPipeline` records `PipelineMetrics`
  (criteria checked · evidence gaps found · precedents considered · reasoning
  steps · assembled-ms — all counted). `BenchmarkService` aggregates across the
  corpus and pairs each row with a **labelled manual estimate + its basis**.
  `GET /api/benchmark`. Dashboard "Cost" tab → **Impact** (Recoverable Value +
  before/after). No improvement % claimed. `Zynara.Core.Tests` +2.
  **88 tests green.**

## Done — 2026-09-06 — P2-3

- **Contradiction-detection flow** (D17). New `claims-extraction` agent
  (stub + Foundry) + deterministic `ContradictionCheck` spoke: pulls assertions
  from the note, pairs affirmed vs negated on the same subject. `noteConflicts > 0`
  → Gate HumanReview + Critic Block + surfaced in the Conflicts panel. Runs after
  evidence-gap (pipeline + orchestrator activity). New `demo-contradiction`
  scenario. `Zynara.Core.Tests` +4. **92 tests green.** (review re-read note #2)

## Done — 2026-09-06 — eval note #3

- **Two eval metrics added** (D18): policy-citation accuracy (draft names the
  governing policy ref, CI floor 95%) + strategy-verdict agreement (`precedent-strategist`
  verdict vs. label, 7 cases, CI floor 80%). Clause hallucination folded into the
  hard-gated hallucination count. Both 100%. **94 tests green.**

## Done — 2026-09-06 — P1-4 infra + CI

- **`infra/`** (D19) — `main.bicep` + 5 modules, `az bicep build` clean, not
  deployed. **Managed-identity only — no VNet / private endpoints** (network
  isolation → TDD §7.1 production hardening). The identity split is the substance:
  `id-reasoning` = Foundry + Cosmos + Blob; `id-submission` = the payer secret +
  Cosmos write, **no Foundry**; local auth off everywhere; one secret in Key
  Vault. Cosmos containers incl. `caseAudit` + `denialCohorts`. `azure.yaml` (azd).
- **`.github/workflows/zynara-ci.yml`** — build + test (eval gate) + bicep build +
  a stale-snapshot check; `Zynara.DemoDump` made deterministic for it.

## Next — remaining (resume point)

**Every evaluator P0 / P1 / P2 finding is addressed.** Remaining, in the order
the user chose: **de-risk spike → `Zynara.Data` → grow eval set → video last.**

1. **De-risk spike — ✅ RUN, PASSED (2026-09-06).** `tools/Zynara.FoundrySpike`
   ran the whole pipeline against hosted GPT-5.4 in project
   `zynara-foundry-28985` / `care-approval` (RG `zynara-spike-rg`). Auth +
   provisioning + 5 agent calls/case + parsing all worked; ~25 s/case. The real
   Critic is stricter than the stub — with File Search **off** it flags "no
   policy clause text" and the clean case routes to Strengthen. **Load the corpus
   (`data/corpus/`, manual — see its README) + set `VECTOR_STORE_ID`** for a
   clean demo run.
2. ~~`Zynara.Data`~~ **done (D20)** — Cosmos-backed `IZynaraStore` +
   `ICaseRepository` + real cost meter; `CosmosJson` round-trip tested;
   `tools/Zynara.DbDeploy` seeder (azd hook); orchestrator + api-proxy wire it on
   `COSMOS_ENDPOINT`. Not run against live Cosmos (no emulator here). **100 tests.**
3. ~~Grow the eval set 8 → ~20~~ **done** — 20 labelled cases now
   (`eval/Zynara.Eval/cases/`): US-region, shoulder procedure, Partial evidence,
   value-just-over-limit, appeal with mixed/all-lost precedents,
   note-self-contradiction. Every metric at 100%, route agreement 100% vs
   baseline 55%, unsafe automation 0 vs baseline 5. **100 tests green.**
   Still open: 2–3 real payer policy files for File Search.
4. **Foundry cleanup — ✅ (D22).** Only the five pipeline agents are provisioned;
   `expiry-watch` / `policy-drift` no longer created as hosted agents (stub twin
   in both modes). User to delete the stale agents from the portal, then re-run
   the spike with `VECTOR_STORE_ID` set.
5. **Precedent corpus** — ✅ (D24, D25). Appeal-losses filtered from a fresh
   submission; P-3 / P-4 (approved-as-submitted) added to `DemoWorld` + corpus
   (7 docs). User to add `precedent-P-3.md` / `P-4.md` to vector store
   `vs_Aw9xAi58sHDnWqxN5kDy5jFJ` and re-run the spike.

### Live data connection — ✅ verified locally (2026-09-06)

`func start` runs `Zynara.ApiProxy` in-memory; `index.html?api=http://localhost:7071`
loads the queue / recovery / benchmark / profiles from it. Confirmed end to end:
every endpoint shape matches the dashboard, `POST /demo/scenarios/{id}/run`
re-runs the pipeline, `POST /cases/{id}/decision` enforces `ApprovalAuthority`
and appends to the audit trail. No Azure, no Cosmos needed. See
`docs/runbooks/local-demo.md`. (Cosmos + hosted-agent paths still only proven by
their own tests / the spike — full stack together is S-4.)

### Still open (not evaluator findings — build + submission work)

| # | Item | Size | Note |
|---|---|---|---|
| ~~S-1~~ | **`Zynara.Submission`** — the outbound adapter — ✅ **done (D27)** | — | Function app on `id-submission` (KV secret + Cosmos write, no Foundry). `POST /api/submit/{id}` sends only a reviewer-approved case, once (idempotent). `SubmissionService` + `CosmosSubmissionStore` + `submissions` container. Stub payer gateway by default; `KeyVaultPayerGateway` reads the credential to prove the boundary. **108 tests.** |
| ~~S-2~~ | **Challenge 2 — agent-keyed traces** — ✅ **done (D26)** | — | `ZynaraTelemetry` ActivitySource: `pipeline.run → spoke.* → invoke_agent * → chat *`. Azure Monitor OTel exporter wired in both Function hosts (gated on `APPLICATIONINSIGHTS_CONNECTION_STRING`). `TelemetryTests` assert the tree. **102 tests.** Visual check at deploy (S-4). |
| ~~S-3~~ | **Challenge 4 — Foundry portal workflow** — ✅ **built + run (2026-09-06)** | — | `care-approval-reasoning` = `evidence-gap → precedent-strategist → critic → End`, ran end to end in preview. Critic node returns `block` (incomplete context on a linear chain) — expected, documented in the runbook as the case for the deterministic orchestrator. Persistent-agents half done via the provisioner. |
| ~~S-4~~ | **Challenge 0 — deploy** — ✅ **DONE (D30, D31)** | — | Live on **`zynara-spike-rg`**. Full flow verified: `POST /api/requests` → Durable orchestrator → `PersistCaseActivity` → Cosmos → dashboard → approve (ApprovalAuthority + audit) → api-proxy `SendCase` → Submission Adapter (reads KV payer credential) → `submissions`. Hosted GPT-5.4 agents on; Challenge 2 traces (`AuthOrchestrator → spoke.* → invoke_agent * → chat gpt-5.4`) confirmed in App Insights. Runbook: `docs/runbooks/deploy.md`. |
| ~~S-5~~ | **`Zynara.Dashboard/index.html`** — light house style — ✅ **done (D29)** | — | The old dark version is deleted; `index.html` is the light-first console (was `standalone.html`). Inlined snapshot by default; `?api=<host>` pulls live cases/recovery/benchmark/profiles and re-renders, falling back to the snapshot on error. |
| S-6 | **Architecture SVG** — `appeal-builder` → `precedent-strategist`, add the contradiction step + the "upstream / not built" band | S | Another session edits this file — coordinate; do not `git add -A`. |
| S-7 | **Pitch + 5-point doc** | M | Submission artifact. |
| S-8 | **Video** | M | **Last**, per the user. |
| S-9 | 2–3 real payer policy files for File Search | S | Corpus is synthetic Bupa today. |
| ~~S-10~~ | **Challenge 3 — Foundry *portal* evaluation** — ✅ **done (D28)** | — | `eval/portal/eval_portal.jsonl` (15 turns for `evidence-gap-agent`) + `build_dataset.py` + `docs/runbooks/challenge-3-portal-evaluation.md`. User runs it in the portal (Evaluate → Evaluations → Create → Agent → Coherence/Fluency), like the vector store. |

## TODO — resume point for session 9

### A · Live deploy — ✅ DONE (2026-09-07, D31)

1–7 all done. `POST /api/requests` → Durable orchestrator → `PersistCaseActivity`
→ Cosmos → dashboard queue → approve (ApprovalAuthority + audit) →
`POST /api/cases/{id}/submit` → Submission Adapter reads the KV payer credential
→ `submissions` container. **Verified live in both `stub` and `foundry` modes.**
Challenge 2 traces confirmed in App Insights: `AuthOrchestrator` → `spoke.*` →
`invoke_agent *` → `chat gpt-5.4` (10s hosted-model spans). Runbook:
`docs/runbooks/deploy.md`. The dashboard has a "run scenario ▾" control and wires
the approve/reject/send buttons to the live endpoints.

**Live URLs:** dashboard `https://proud-water-0e35b1603.6.azurestaticapps.net` ·
api-proxy behind the SWA proxy · orchestrator
`func-zynara-orchestrator-itbahognjguwy.azurewebsites.net/api/requests`.

### B · Submission artifacts (the remaining work)
8. **S-7 — pitch + 5-point doc.** The four review questions (§20), the challenge
   mapping, the eval numbers, the live URL.
9. **S-6 — architecture SVG** (other session owns the file): `appeal-builder` →
   `precedent-strategist`, add the contradiction step, add the "upstream / not
   built" band.
10. **S-9 (optional)** — 2–3 real public payer policy docs into the corpus.
11. **S-8 — the 3-min video.** Last. Script is `docs/runbooks/demo-script.md`.

## Deploy state — LIVE (2026-09-06, end of session 8)

**Resource group `zynara-spike-rg`** (one RG, alongside the pre-existing Foundry
account `zynara-foundry-28985` / project `care-approval`). Env: `zynara-hack`.

**Working, verified in Azure:**
- API (Function app `func-zynara-apiproxy-itbahognjguwy`) — every endpoint 200
  **through the Static Web App proxy**: `health`, `cases`, `recovery`, `benchmark`,
  `profiles`, `demo/scenarios`.
- **Orchestrator end-to-end** — `POST /api/requests` on
  `func-zynara-orchestrator-itbahognjguwy` started a Durable orchestration that
  ran to **Completed**, reading criteria + precedents from **Cosmos**, running the
  Gate. This is the real proof the Durable path + hub storage + activities work.
- Cosmos `careapproval` seeded — criteria 2, precedents 5, denialCohorts 2.
- Dashboard live: `https://proud-water-0e35b1603.6.azurestaticapps.net`
- `agentsMode = stub` on the deploy (Foundry flip is a later step).

**The shakedown fixes (all now in bicep + committed, D30 pending):**
CognitiveServices API `2025-06-01`; suffix seeded on RG; SWA → `westeurope`;
explicit `Newtonsoft.Json`; deployer + app identities' Cosmos data-plane roles;
`Newtonsoft` runtime dep; Consumption Y1 + TireForge storage pattern
(blob/queue/table roles); **`AZURE_CLIENT_ID`** on each app so the code's
`DefaultAzureCredential` resolves the user-assigned identity; dashboard
`package.json`; `azd deploy` **one service at a time** (Codespace OOMs on 3
parallel `dotnet publish`).

**2 gaps left (next session):**
1. **Dashboard queue is empty** (`/api/cases` → `[]`). `AuthOrchestrator` returns
   the `PipelineResult` but never persists a `CaseRecord` to the `cases`
   container — only the api-proxy's `CaseService.RunAsync` does. Fix: add a
   persist activity to the orchestrator, or fire `POST /api/demo/scenarios/{id}/run`
   against the live api-proxy to populate the queue.
2. **Easy Auth on the api-proxy** — the SWA linked backend auto-enabled it, so
   direct calls to `func-zynara-apiproxy-*.azurewebsites.net` 401; only the SWA
   proxy path works. Expected SWA behaviour, not a bug — document it, or drop the
   linked backend + use `?api=` if direct access is wanted.

**Running cost (idle, rough):** Static Web App **Standard ≈ $9/mo** (the only
fixed cost) · Cosmos serverless, 3× Consumption Functions, 2× Storage — a few $/mo
· App Insights + Log Analytics — pay-per-GB, low for a demo · Foundry gpt-5.4
deployment — $0 while `agentsMode=stub`. **≈ $10–15/mo total.** To zero it:
`azd down` (keeps the existing Foundry — it's `existing` in bicep), or SWA → Free.

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
