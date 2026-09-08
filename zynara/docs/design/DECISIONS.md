# Care Approval IQ — decision log

Deltas from the design of record (`ARCHITECTURE.md`) and the TDD, newest first.
Each entry: what changed, why, and what it touched.

---

## D37 · Rename the Gate route `AutoSubmit` → `ReadyToSubmit`

**2026-09-08.** The V3.1 re-evaluation (§11) flagged the label `AutoSubmit` as a
scan-time hazard: it *visually* reads as autonomous payer submission even though
D35 already made every send pause for a human. D35 chose to keep the name ("it is
still the Gate's *assessment*"); this reverses that — the small ambiguity is not
worth leaving in front of a judge.

`GateRoute.AutoSubmit` → `GateRoute.ReadyToSubmit` (PascalCase, so the
`[JsonStringEnumConverter]` wire value is `"ReadyToSubmit"`). Also
`GateDecision.AutoSubmit` bool → `ReadyToSubmit`; `ApprovalAuthority.AutoSubmitLimit`
→ `ReadyToSubmitLimit`. The Gate's own reason string and the reviewer headline now
say "ready to submit — a reviewer approves the send". The display status was
already `CaseStatus.ReadyToSubmit` — the route name now matches it.

Touched: `Zynara.Core` (Results, Gate, CaseViewBuilder, ApprovalAuthority,
AuthPipeline, DemoCatalog), `Zynara.Workflow`, `Zynara.Agents` stubs + prompts,
`eval/` (3 case JSONs + runner + baseline), all affected tests, the dashboard
(`index.html` route→style map + inlined snapshot, regenerated `demo-cases.json`),
the architecture SVG, and the current-state docs. **Build + 119 tests green.**

Swept in alongside — two latent snapshot-determinism bugs that only surfaced
running `DemoDump` on Windows: `AuthPipeline` / `StubPrecedentStrategistAgent`
draft builders now `.ReplaceLineEndings("\n")`, and the strategist's win-rate `P0`
format is pinned to `InvariantCulture` — so the committed `demo-cases.json` is
byte-identical regardless of the OS that regenerates it.

## D36 · MAF migration Phase 5 — retire the v1 Durable orchestrator

**2026-09-08.** With the MAF Workflow host (`Zynara.WorkflowHost`) verified live
(Phase 4, D33), the v1 Durable Functions orchestrator is dead code. Removed:

- `src/Zynara.Orchestrator/` (the `AuthOrchestrator` hub, `SpokeActivities`,
  `Http/RequestEndpoints`, `ActivityInputs`) and `tests/Zynara.Orchestrator.Tests/`
  (incl. `FakeOrchestrationContext`). The MAF graph in `src/Zynara.Workflow`
  (`InProcessExecution` tests) is the reference pipeline now; `AuthPipeline` stays
  as the in-process assembler the durable `assemble` executor wraps.
- Both projects out of `Zynara.sln`; the `orchestrator` service out of
  `azure.yaml`; the `orchestrator` Function app + `orchestratorName` var +
  `ORCHESTRATOR_APP_NAME` output out of `infra/` (`modules/apps.bicep`,
  `main.bicep`). The live `func-zynara-orchestrator-<suffix>` app is deleted
  out-of-band (azd will not remove an app whose bicep is gone).

Also swept in the same pass: empty placeholder dirs
(`tests/Zynara.ApiProxy.Tests`, `tests/Zynara.TestSupport`), redundant `.gitkeep`
files in populated dirs, and the superseded TDD generators — `build-tdd-v2-docx.py`
+ `tdd-v2-figures/` deleted, `build-tdd-v3-docx.py` → `build-tdd-docx.py`
(`DEST` = `Care-Approval-IQ-TDD.docx`), `tdd-v3-figures/` → `tdd-figures/`. The
old single-version `Care-Approval-IQ-TDD-V{2,3}.docx` + `*_DesignV2.*` diagrams +
evaluator-review docx were removed by the author.

Touched: as above + light-touch doc updates (`ARCHITECTURE.md` mermaid node,
`infra/README.md`, `Care-Approval-IQ-TDD.md` §2, `challenge-4-portal-workflow.md`,
`STATUS.md`). Deep TDD §12 rewrite (TD-1a, TD-1/TD-2 for MAF) stays with the S-6
doc/SVG pass. **Build + tests to be re-verified in the Codespace** (no toolchain
on the authoring machine).

## D35 · Every send pauses for a human — `AutoSubmit` no longer auto-sends

**2026-09-07.** The re-evaluation (`Evaluate Review V2.docx`) flagged a real
contradiction: the TDD said *"every outbound action is human-approved"* but the
`AutoSubmit` route in `CareApprovalWorkflow.BuildDurable` went to an `auto-approve`
executor that recorded a `"system"` decision and submitted with **no human pause**.

Fixed the architecture, not the wording. `BuildDurable` now routes **every** case
that needs a submission through the `review` RequestPort; the `auto-approve`
executor is deleted. The Gate route only sets the reviewer's headline —
`AutoSubmit` → *"ready — one click to send"*, `HumanReview` → *"needs your
judgement"*, etc. (a new `Headline(GateRoute)` helper). Cases where prior auth is
not required end at a `finalize` node with no reviewer. `GateRoute.AutoSubmit`
keeps its name (it is still the Gate's *assessment*); only the workflow behaviour
changed. Now literally true: **no payer submission without an explicit human
`/respond`.**

Touched: `src/Zynara.Workflow/CareApprovalWorkflow.cs`,
`tests/Zynara.Workflow.Tests/ReviewPortTests.cs` (the "sent by the system with no
pause" test → "a ready case still pauses for a one-click approval"), the V2 SVG
orchestrator strip, `docs/design/tdd-v2-figures/fig-pipeline-flow.svg`, the V2 TDD
(§2, §5, §6). **122 tests green.** *Live redeploy of `workflowhost` pending.*

## D34 · Re-evaluation follow-ups — surface the evidence that already exists

**2026-09-07.** The re-eval put the submission at 8.9/10 with a path to 9.3+. Of
its six asks, four were already built and only needed surfacing:

- **Agent → executor relationship** — documented (V2 TDD §2): the agent reasons
  in prose and returns a typed recommendation; the deterministic executor
  validates it (catching an off-record criterion / precedent), normalises it, and
  commits it to the Gate's contract.
- **20-case eval result** — the `Zynara.Eval` scorecard is now a table in the V2
  TDD §3 (precision/recall 100%, mandatory FN 0/3, hallucinated refs 0, route
  agreement 100%, safe abstention 2/2, unsafe automation 0).
- **Multi-agent vs. one generalist** — surfaced from the existing
  `GeneralistBaseline`: route agreement pipeline 100% / baseline 55%; unsafe
  automations pipeline 0 / baseline 5. The `EvalGateTests` already fail the build
  if the pipeline is not measurably safer.
- **"CI-gated" is real** — `.github/workflows/zynara-ci.yml` already runs
  `dotnet test` (incl. the eval gate) on every push; the V1 analysis note that it
  was missing was wrong (checked from the wrong directory).
- **Wording** — the eval is framed as "20 labelled *synthetic* cases, route
  agreement + safety metrics", never a headline accuracy figure.

Still open (for the video / a later pass): the Critic visibly challenging a case
on camera; a richer per-dimension baseline table (the straw-man only emits a
route today); optionally growing the case set toward ~40–50.

Touched: `scripts/build-tdd-v2-docx.py` + `Care-Approval-IQ-TDD-V2.docx`.

## D33 · MAF migration Phases 0–3 done — checkpoint

**2026-09-07.** `maf-migration` branch, 5 commits, **122 tests green**, `main`
(v1) untouched and still deployed. Full detail: `docs/design/MAF-MIGRATION.md`
§§5a–3.

- **Phase 0 (spike) — GO.** `src/Zynara.Workflow`. Executors are ~3-line lambdas
  over the *unchanged* `Zynara.Core.Pipeline.*` classes
  (`handler.BindAsExecutor(id)`); a `PipelineState` record accumulates down the
  spine. `Microsoft.Agents.AI.Workflows` **1.20.0 stable** carries Phases 0–1.
- **Phase 1 (graph).** `CareApprovalWorkflow.Build` — the whole assembly pipeline
  as a graph, producing the same `PipelineResult` v1's `AuthPipeline` did.
  `CareApprovalRunner` is a drop-in for `AuthPipeline.RunAsync`.
- **Phase 2 (agents).** `FoundryAgentClient.InvokeAsync` now runs through
  `AIProjectClient.AsAIAgent(new AgentReference(name))` + `agent.RunAsync(prompt)`
  — bound to the *existing* server-side agents. The 5 `Foundry*Agent` classes are
  unchanged. Packages: `Microsoft.Agents.AI` 1.20.0 + `.Foundry` preview;
  `Azure.AI.Projects` bumped 2.0.1 → 2.1.0-beta.4 (MAF's floor). **Verified
  against real GPT-5.4** — `tools/Zynara.FoundrySpike` runs both the v1 pipeline
  and the MAF workflow per scenario and the routes match on both.
- **Phase 3 (HITL).** `CareApprovalWorkflow.BuildWithReview` — after the Gate the
  case is persisted, an AutoSubmit route is sent by the system, every other route
  **pauses** at an `AddExternalCall<ReviewCard, ReviewDecision>` port. On resume,
  `apply-decision` enforces `ApprovalAuthority` (audits refusals) and hands an
  authorised `approve-send` to the Submission Adapter.

**Key learnings (fed back to the plan):**
1. The `Zynara.Core.Agents` *ports* + stub twins are already the test seam — no
   fake `IChatClient` needed; the stub path never changed.
2. MAF shared state (`QueueStateUpdateAsync`) is **per-executor** unless a
   `scopeName` is given — a named scope is required to carry the request id past
   the review pause.
3. `FoundryChatClient` has no public ctor — `AIProjectClient.AsAIAgent(AgentReference)`
   is the entry point, and it **binds to** an existing agent, it does not create one.
4. The preview `.DurableTask` / `.Hosting.AzureFunctions` packages lag at
   1.16-preview vs 1.20 core — the version-skew risk to manage in Phase 4.

Deferred to Phase 4: durable hosting + a Durable Task Scheduler in `infra/`, the
dashboard retarget to `/respond/{runId}`, redeploy, merge to `main`.

## D32 · v1 baseline tagged; orchestration moves to Microsoft Agent Framework

**2026-09-07.** The Durable Functions implementation is **complete, deployed and
green** (108 tests, full flow verified live — D31). Tagged **`v1.0-durable`** as
the working baseline.

**Decision: rebuild the orchestration layer on the Microsoft Agent Framework
(MAF).** Not a compromise for the deadline — a deliberate investment. This repo
is the **reference pattern for the team's forthcoming agent projects**, and MAF
(the Semantic Kernel + AutoGen successor) is Microsoft's recommended pattern:
a typed Workflow graph of executors, built-in checkpointing / human-in-the-loop
ports / OpenTelemetry, native Foundry-agent binding.

**What the earlier TD-1 got wrong.** It rejected *"agents orchestrating via
connected agents"* — a strawman. MAF Workflows are a **deterministic typed
graph**, not free-form agent chatter; a deterministic Gate node and an explicit
human seam are first-class. The hybrid principle (deterministic code owns every
decision value; agents produce prose) is *preserved* — it just becomes graph
executors instead of Durable activities.

**What stays unchanged:** `Zynara.Core` domain model, the Gate logic, the
structured decision model, `ApprovalAuthority`, the eval suite, `Zynara.Data`,
`Zynara.Submission`, the dashboard, the infra. The migration is confined to the
orchestration + agent-invocation layer.

**Plan:** `docs/design/MAF-MIGRATION.md` (target architecture + phased steps).
Durable Functions may remain as the *host* (durable timers for `expiry-watch`,
multi-day durable wait for the payer round-trip) with a MAF Workflow as the
pipeline — decided in the migration doc.

## D31 · Live deploy finished — full flow + hosted agents verified (S-4)

**2026-09-07.** Session 9. The deploy is demo-ready:

- **`AuthOrchestrator` persists the case** — extracted `CaseService.PersistAsync`
  (project + audit + save from an already-computed `PipelineResult`) and added a
  `PersistCaseActivity` after Draft (and on the not-required early stop), so
  `POST /api/requests` populates the same review queue the api-proxy path does.
- **Dashboard is interactive in live mode** — a "run scenario ▾" control
  (`POST /api/demo/scenarios/{id}/run`), and the approve / reject / send buttons
  hit the live endpoints: `POST /api/cases/{id}/decision` (with `X-Reviewer-Role`)
  then, on approve-send, `POST /api/cases/{id}/submit`. Live mode auto-detects a
  same-origin `/api` (the SWA linked backend), no `?api=` needed.
- **`SendCase`** on the api-proxy (`/api/cases/{id}/submit`) forwards to the
  Submission Adapter at `SUBMISSION_URL` — the dashboard talks to one origin only.
  `/api/submit/{id}` is Anonymous (guarded by `SubmissionService`).
- **Hosted agents flipped on** (`AGENTSMODE=foundry`) — the reasoning identity
  already had Cognitive Services User on the existing Foundry account and the 5
  agents already exist there, so no re-provisioning. A live orchestration ran the
  real GPT-5.4 pipeline; App Insights shows the nested trace
  `AuthOrchestrator → spoke.evidence-gap → invoke_agent evidence-gap → chat gpt-5.4`
  (~10 s model span) — Challenge 2 proven in production.
- `docs/runbooks/deploy.md` written (steps + the D30 shakedown fixes + the two
  known SWA behaviours: Easy Auth on the api-proxy, Anonymous `/api/submit`).

Touched: `Zynara.Core/View/CaseService.cs`, `Zynara.Orchestrator/Orchestration/*`,
`Zynara.ApiProxy/{CaseFunctions,Program}.cs`, `Zynara.Submission/SubmissionFunctions.cs`,
`Zynara.Dashboard/index.html`, `infra/modules/apps.bicep`,
`tests/Zynara.Orchestrator.Tests/*`. **108 tests green.**

## D30 · Live deploy — one RG, existing Foundry, Consumption Y1 (S-4)

**2026-09-06.** First `azd up` to a real subscription. Decisions taken under fire:

- **One resource group** (`zynara-spike-rg`) holding everything, next to the
  pre-existing Foundry account from the spike. `foundry.bicep` was rewritten to
  **reference** the existing `zynara-foundry-28985` / `care-approval` (+ its
  `gpt-5.4` deployment) rather than create a new one — it now only adds Log
  Analytics + App Insights + the Cognitive Services User grant.
- **Consumption Y1 Linux** for the Function apps (was EP1). EP1 + keyless host
  storage cannot work (needs an Azure Files content share, needs the key). Y1 +
  the TireForge storage pattern: identity-based `AzureWebJobsStorage`
  (blob/queue/table URIs + the UAMI `clientId`), the content share on the account
  key, Blob Data Owner + Queue + Table roles for both app UAMIs.
- **`AZURE_CLIENT_ID`** app setting per app — the code's `DefaultAzureCredential`
  cannot pick a user-assigned identity without it (`ManagedIdentityCredential:
  Unable to load the proper Managed Identity`, 400).
- **`Newtonsoft.Json`** referenced explicitly (Cosmos SDK 3.x runtime need).
- Deployer principal (`AZURE_PRINCIPAL_ID`) granted Cosmos Data Contributor so
  the `postprovision` seed hook works (Cosmos `disableLocalAuth`).
- Static Web App pinned to `westeurope` (not offered in the Nordics); name suffix
  seeded on the RG, not the env name; CognitiveServices API `2025-06-01`.
- `azd deploy` one service at a time — the Codespace OOMs on 3 parallel `dotnet publish`.

**Verified in Azure:** the API answers through the SWA proxy, and `POST
/api/requests` on the orchestrator ran a Durable orchestration to completion
against Cosmos. Two gaps recorded in STATUS "Deploy state": the orchestrator
doesn't persist a `CaseRecord` (empty dashboard queue), and the SWA linked
backend auto-enabled Easy Auth on the api-proxy.
Touched: `infra/main.bicep`, `infra/modules/{foundry,apps,data,identity}.bicep`,
`infra/main.parameters.json`, `Directory.Build.props`,
`src/Zynara.Data/Zynara.Data.csproj`, `src/Zynara.Dashboard/package.json`.

## D29 · One dashboard file — `index.html` is the light console (S-5)

**2026-09-06.** The repo carried two dashboards: `index.html` (the original
dark version, rejected) and `standalone.html` (the light house-style rebuild the
user approved). Deleted the dark one; `standalone.html` is now `index.html` — the
single file the Static Web App serves and the artifact publishes. It renders from
the inlined `<script id="demo-data">` snapshot by default; `?api=<ApiProxy host>`
pulls live `cases` / `recovery` / `benchmark` / `profiles` / `demo/scenarios` and
re-renders, falling back to the snapshot on any fetch error.
Touched: `src/Zynara.Dashboard/{index.html,README.md}` (deleted `standalone.html`).

## D28 · Challenge 3 — the Foundry portal evaluation half (S-10)

**2026-09-06.** Challenge 3 was only half covered: `eval/Zynara.Eval` (CI,
20 cases, hard-gated) is the *correctness* half; the Foundry *portal* evaluation
— the analog to what TireForge did — was missing. Added
`eval/portal/eval_portal.jsonl` (15 turns for `evidence-gap-agent`, each `query`
is the exact prompt `FoundryEvidenceGapAgent` builds; `context` + `ground_truth`
for optional Groundedness / Similarity), `eval/portal/build_dataset.py`
(deterministic regen), `eval/portal/README.md`, and
`docs/runbooks/challenge-3-portal-evaluation.md` (Evaluate → Evaluations →
Create → Agent → Coherence/Fluency). Manual run, like the vector store.
Touched: `eval/portal/*`, `docs/runbooks/challenge-3-portal-evaluation.md`, TDD §10.

## D27 · `Zynara.Submission` — the outbound adapter (S-1)

**2026-09-06.** The infra + `azure.yaml` referenced `src/Zynara.Submission` but
the project did not exist. Built it as the sole outbound path:

- `Zynara.Core`: `SubmissionRecord` / `SubmissionStatus`, `ISubmissionStore`
  (+ in-memory), `IPayerGateway` + `StubPayerGateway` (deterministic ack), and
  `SubmissionService.SubmitAsync` — enforces the two safety rules: **only a case
  a reviewer marked `approve-send` is sent**, and a **second call is idempotent**
  (returns the existing record, never re-sends). Emits a `submission.send` span.
- `Zynara.Data`: `CosmosSubmissionStore`, `submissions` container (pk
  `/requestId`) added to `CosmosNames` + `infra/modules/data.bicep`.
- `Zynara.Submission` (Function app): `POST /api/submit/{requestId}` (function
  auth), `GET /api/submissions[/{id}]`. `KeyVaultPayerGateway` reads
  `payer-integration-credential` from Key Vault to prove the identity boundary,
  then acknowledges (the real X12/FHIR call is out of scope, marked in code).
  Wired for Cosmos + Key Vault + the OTel exporter on config; stub gateway +
  in-memory stores otherwise. No `PROJECT_ENDPOINT` — this app never calls a model.

`SubmissionServiceTests` (5) + a Cosmos round-trip. **108 tests green.**
Touched: `Zynara.Core/{Model/SubmissionRecord,Abstractions/ISubmissionStore,
Submission/*}.cs`, `Zynara.Core/DependencyInjection.cs`, `Zynara.Data/*`,
new `src/Zynara.Submission/*`, `Zynara.sln`, `azure.yaml`, `infra/modules/data.bicep`.

## D26 · Challenge 2 — agent-keyed traces (`ZynaraTelemetry` ActivitySource)

**2026-09-06.** Designed in the TDD (§10) but not built. Added
`Zynara.Core.Diagnostics.ZynaraTelemetry` — one `ActivitySource` ("Zynara.Pipeline")
with helpers that emit a nested span tree: `pipeline.run` (tags: request id,
procedure, payer, region, is-appeal, route, reasoning steps) → `spoke.<name>`
(needs-auth · evidence-gap · claims-extraction · precedent-match · critic) →
`invoke_agent <name>` (only when the spoke actually calls its agent) →
`chat <model>` (added by `FoundryAgentClient`, hosted path only, tags the token
counts + tool calls). `AuthPipeline` and the five spokes are instrumented; the
Durable activities inherit the same spans per invocation.

The Function hosts (`Zynara.Orchestrator`, `Zynara.ApiProxy`) register the source
with the **Azure Monitor OpenTelemetry exporter**
(`Azure.Monitor.OpenTelemetry.Exporter` + `OpenTelemetry.Extensions.Hosting`)
when `APPLICATIONINSIGHTS_CONNECTION_STRING` is set — otherwise the spans are
still emitted (for tests) but not shipped. `infra/modules/apps.bicep` already
wires that setting. `TelemetryTests` (2) assert the tree with an `ActivityListener`.
**102 tests green.** Visual verification in the App Insights transaction view
happens at deploy (S-4).
Touched: `Zynara.Core/Diagnostics/ZynaraTelemetry.cs`, `Pipeline/{AuthPipeline,
NeedsAuthCheck,EvidenceGapMatch,ContradictionCheck,AppealMatch,CriticCheck}.cs`,
`Zynara.Agents/Foundry/FoundryAgentClient.cs`, both Function `Program.cs` (+2 pkgs each),
`tests/Zynara.Core.Tests/TelemetryTests.cs`, TDD §7/§10.

## D25 · Precedent corpus gains first-time-approval cases; support model widened

**2026-09-06.** Follow-on from D24. Every precedent on record was an appeal case,
so on a clean first submission the hosted Critic still had nothing to treat as
positive support and questioned any reliance on appeal-overturn cases. Added
**P-3 / P-4** — comparable MRI-LS cases *approved as submitted* (`InitiallyApproved`,
`NotAppealed`) — to `DemoWorld` and the File Search corpus (7 docs now).
`AppealMatch` now scores a precedent as **favourable** if it was approved as
submitted *or* won on appeal (was: won on appeal only) — this drives the
similarity bonus and `DeriveSupport`. `StubPrecedentStrategistAgent` cites the
approved-as-submitted cases on a Submit verdict and the appeal wins on an Appeal
verdict. `avgPrecedentsConsidered` 2 → 3.7; demo snapshot regenerated.
Touched: `Zynara.Core/Demo/DemoWorld.cs`, `Pipeline/AppealMatch.cs`,
`Zynara.Agents/Stubs/StubPrecedentStrategistAgent.cs`, `data/corpus/*`,
`Zynara.Dashboard/{demo-cases.json,standalone.html}`, `config/profiles/*`.

## D24 · Appeal-losses excluded from the shortlist on a fresh submission

**2026-09-06.** Surfaced by the hosted de-risk spike: on `demo-ready` (no denial,
every criterion documented) the hosted Critic flagged that a lost precedent
(P-9) was sitting in the shortlist as "support", and routed the clean case to
Strengthen instead of AutoSubmit. A precedent that lost on appeal is only
decision-relevant when weighing whether to *appeal* — on a first submission it is
noise. `AppealMatch.RunAsync` now filters `AppealOutcome.AppealLost` out of the
candidate set unless `request.DenialLetter` is present. `avgPrecedentsConsidered`
in the benchmark drops 2.57 → 2; demo snapshot regenerated.
Touched: `Zynara.Core/Pipeline/AppealMatch.cs`,
`Zynara.Dashboard/{demo-cases.json,standalone.html}`, `config/profiles/*`.

## D23 · `appeal-builder` renamed to `precedent-strategist`

**2026-09-06.** The agent never "builds an appeal" on most cases — it reads the
payer's recorded outcomes for comparable cases and recommends submit / strengthen
/ appeal (it only drafts a letter in the denial branch). Full rename, not just
the display label: `IAppealBuilderAgent` → `IPrecedentStrategistAgent`,
`{Foundry,Stub}AppealBuilderAgent` → `…PrecedentStrategistAgent`,
`AppealRecommendation` → `StrategyRecommendation`, `AppealVerdict` →
`StrategyVerdict` (values Submit/Strengthen/Appeal unchanged), hosted agent name
`appeal-builder-agent` → `precedent-strategist-agent`, roster step, prompt,
eval `expectedAppealVerdict` → `expectedStrategyVerdict`, metric label. The
`AppealMatch` deterministic spoke (precedent scoring) keeps its name.
Touched: `Zynara.Core/Agents/Contracts.cs`, `Model/{Results,Domain}.cs`,
`Pipeline/{AppealMatch,AuthPipeline}.cs`, `Zynara.Agents/*` (2 files renamed),
`eval/Zynara.Eval/*` + `cases/*.json`, `Zynara.Dashboard/{demo-cases.json,standalone.html}`,
tests, TDD/ARCHITECTURE/STATUS/review docs. **100 tests green.** Architecture SVG
still says "appeal-builder" — flagged for the diagram owner.

## D22 · Foundry provisions only the five pipeline agents

**2026-09-06.** `expiry-watch` and `policy-drift` are deterministic monitors
(D6) — the pipeline never calls them and the stub twin *is* the narrator. They
were still being registered as hosted agents and created in the Foundry project,
so the portal showed seven agents where the architecture has five. Now
`FoundryAgentProvisioner.Specs` and `FoundryAgentOptions.AllAgentNames` list
only needs-auth, evidence-gap, claims-extraction, precedent-strategist, critic; in
`foundry` mode `IExpiryWatchAgent` / `IPolicyDriftAgent` resolve to their stubs.
The interfaces, `ExpiryMath` / `PolicyDiff` spokes and stubs stay for the
(unwired) Early Warnings feature.
Touched: `Zynara.Agents/Foundry/{FoundryAgentProvisioner,FoundryAgentOptions,AgentPrompts}.cs`,
`Zynara.Agents/DependencyInjection.cs`, deleted `Foundry/Foundry{ExpiryWatch,PolicyDrift}Agent.cs`,
`tests/Zynara.Agents.Tests/FoundryResponseTests.cs`.

## D21 · Eval set grown to 20 labelled cases

**2026-09-06.** `eval/Zynara.Eval/cases/` 8 → 20. New coverage: US region,
a second procedure (shoulder), Partial evidence, value just over the auto-limit,
appeal with a shared denial code, appeal with all-lost precedents, and a
note-self-contradiction. Every metric at 100%; route agreement 100% vs the
generalist baseline's 55%; unsafe automation 0 vs the baseline's 5.
Touched: `eval/Zynara.Eval/cases/case-09..20.json`.

## D20 · Zynara.Data — Cosmos-backed store, case repository, cost meter

**2026-09-06.** `src/Zynara.Data` (`Microsoft.Azure.Cosmos` 3.62, STJ serializer,
`DefaultAzureCredential`):

- `CosmosZynaraStore : IZynaraStore` — reference data (criteria / rules /
  precedents / policy versions / denial cohorts) + the advisory early-warnings.
- `CosmosCaseRepository : ICaseRepository` — one doc per request id in `cases`
  (the assembled case + its audit trail, D15).
- `CosmosAgentCallRecorder : IAgentCallRecorder` — the real cost meter, one doc
  per hosted-agent call partitioned by day.
- Docs are the domain records serialised straight through (`CosmosJson.WithId`) +
  an `id` — no wrapper types. `Zynara.Data.Tests` round-trips a full `CaseRecord`.
- `DataSeeder` + `tools/Zynara.DbDeploy` (the azd `postprovision` hook) — create
  the 9 containers (`CosmosNames.All`, matched to `infra/modules/data.bicep`) and
  seed the demo reference data if empty.
- `AddZynaraData(endpoint, database)`. The orchestrator + api-proxy `Program.cs`
  call it when `COSMOS_ENDPOINT` is set, otherwise keep the in-memory `DemoWorld`.
- `Directory.Build.props` sets `AzureCosmosDisableNewtonsoftJsonCheck` repo-wide.

**100 tests green.** Not run against a live Cosmos (no emulator in the Codespace).

## D19 · infra/ skeleton + CI — identity boundary only (no private networking)

**2026-09-06.** Evaluator P1-4 / §13. `infra/main.bicep` (+ 5 modules) —
`az bicep build` clean, not deployed. **Managed-identity first; no VNet / private
endpoints / NSGs** — the network isolation is production hardening (TDD §7.1),
and the review guardrail (§18) is explicit about not adding Azure services for an
enterprise look. The substance that matters — a reasoning agent cannot reach the
payer credential — is the **identity split**:

- one user-assigned managed identity per component: `id-reasoning`
  (orchestrator + api-proxy), `id-submission` (the Submission Adapter),
  `id-dashboard`.
- `id-reasoning` → Cognitive Services User (inference) + Cosmos data-plane +
  Blob read. `id-submission` → the payer-integration secret (Key Vault Secrets
  User) + Cosmos write, and **no Foundry** (its app has no `PROJECT_ENDPOINT`).
  `id-dashboard` → nothing.
- No stored connection strings: Cosmos `disableLocalAuth`, Storage
  `allowSharedKeyAccess: false`, the one secret in Key Vault.
- `data.bicep` Cosmos containers: requests · submissions · outcomes · **caseAudit**
  (D15) · agentCalls · **denialCohorts** (D12).
- `azure.yaml` (azd) wires orchestrator / apiproxy / submission / dashboard.

`.github/workflows/zynara-ci.yml` — build + `dotnet test` (the eval gate runs
here) + `bicep build` + a stale-snapshot check. `Zynara.DemoDump` made
deterministic (fixed `TimeProvider`, `AvgAssembledInMs` zeroed in the snapshot)
so that check is stable.

Touched: `infra/*`, `azure.yaml`, `.github/workflows/zynara-ci.yml`,
`tools/Zynara.DemoDump`, `Zynara.Core/Impact/BenchmarkService.cs`,
`src/Zynara.Dashboard/standalone.html`, TDD §7.1.

## D18 · Eval — policy-citation accuracy + strategy-verdict agreement

**2026-09-06.** Review re-read note #3. `EvalGroundTruth` gains
`ExpectedStrategyVerdict` (Submit/Strengthen/Appeal) and `ExpectedPolicyRef`.
`EvalRunner` now also scores: **policy-citation accuracy** (the assembled draft
names the governing policy ref — CI floor 95%), **strategy-verdict agreement** (the
`precedent-strategist` verdict vs. the label, 7 labelled cases — CI floor 80%), and
folds **clause hallucination** (a `under/clause/citing x.y` in the appeal draft
that no shortlisted precedent cited) into the hard-gated hallucination count.
Current run: both 100%, 0 hallucinations, 8 eval tests.
Touched: `eval/Zynara.Eval/{EvalCase,EvalRunner,EvalReport,EvalGateTests}.cs`,
`cases/*.json`, TDD §7.3.

## D17 · Contradiction-detection flow — claims → pair → never resolved silently

**2026-09-06.** Evaluator finding #4 / P2-3 / review re-read note #2. P0-3 only
gave us "evidence contradicts a *criterion*"; this adds the note-against-itself
check the review's flowchart draws.

- New `IClaimExtractionAgent` (stub + Foundry, prompt `AgentPrompts.Claims`) —
  pulls discrete assertions `{ text, subject, negated }` from the clinical note.
- New deterministic spoke `ContradictionCheck` — groups claims by subject; a
  subject with both an affirmed and a negated claim is a `ClaimConflict`.
- Runs after `evidence-gap`, before `appeal-match` (pipeline + orchestrator
  `ContradictionActivity`). Feeds the Gate (`noteConflicts > 0` → **HumanReview**,
  before the appeal/mandatory checks — never picked apart automatically), the
  Critic (check 4 also weighs `NoteConflicts` → Block), and the reviewer
  Conflicts panel.
- New demo scenario `demo-contradiction`: one sentence says physiotherapy was
  completed, another that the patient could not attend → routed to a human.

Touched: `Zynara.Core/Agents/ClaimContracts.cs`, `Pipeline/ContradictionCheck.cs`,
`Model/Results.cs` (`ContradictionResult`), `Gating/Gate.cs`, `Pipeline/{AuthPipeline,CriticCheck}.cs`,
`Agents/Contracts.cs` (`CriticContext.NoteConflicts`), `View/CaseViewBuilder.cs`,
`Zynara.Agents/{Stubs,Foundry}` (+ provisioner, +1 agent → 7 Specs, roster),
`Zynara.Orchestrator` (activity + inputs), `Demo/DemoCatalog.cs`,
`Zynara.Core.Tests` (+4), `Zynara.Agents.Tests`, `Zynara.Orchestrator.Tests`.
**92 tests green.**

## D16 · Before / after benchmark — measured pipeline, labelled manual estimate

**2026-09-06.** Evaluator P2-2 / §16. `AuthPipeline` now records
`PipelineMetrics` per run (criteria checked, evidence gaps found, precedents
considered, reasoning steps, assembled-in-ms — all counted, none estimated).
`BenchmarkService` aggregates these across the case corpus and pairs each row
with a **manual estimate that is marked as an estimate and carries its basis**
(AMA prior-auth burden survey, the appeals-gap statistic, etc.). `GET /api/benchmark`;
dashboard "Cost" tab → **Impact** (Estimated Recoverable Value + the before/after
table). **No improvement percentage is claimed** (guardrail §18).
Touched: `Zynara.Core/Model/Results.cs` (`PipelineMetrics`), `Pipeline/AuthPipeline.cs`,
`Zynara.Core/Impact/*`, Core DI, `Zynara.ApiProxy/BenchmarkFunctions.cs`,
`tools/Zynara.DemoDump`, `src/Zynara.Dashboard/standalone.html`,
`Zynara.Core.Tests` (+2).

## D15 · Reviewer roles + approval authority + a case audit trail

**2026-09-06.** Evaluator P1-3 / §13 (+ review notes #4 tiered thresholds, #5
agent version in the audit record).

- `ApprovalAuthority` (`Zynara.Core/Authority`) — four ranked roles
  (Coordinator/Reviewer/SeniorReviewer/MedicalDirector). The role required to
  **approve & send** = max(route tier, financial-risk tier); an appeal is always
  ≥ Senior. The Gate's auto-limit is the bottom tier — over it a case escalates
  to a more senior human, not a bigger number.
- `CaseRecord.Audit` — append-only `AuditEntry` list: the pipeline run + each
  reasoning step's agent implementation **and version** (via `IAgentRoster` in
  `Zynara.Agents`: stub → `deterministic`, foundry → `hosted/<model>`), every
  reviewer action (identity · role · timestamp · note), and every **refusal**
  with its reason.
- `CaseService.RecordDecisionAsync` returns a `DecisionOutcome` (found / allowed /
  refusal); `Zynara.ApiProxy` reads the role from `X-Reviewer-Role` (demo
  stand-in for Entra app roles) → 403 on refusal; `GET /api/cases/{id}` now
  returns `audit`. `CaseView` gains `ApproveAuthority` + `Audit`.
- Dashboard: a role picker in the masthead; the case drawer shows the audit trail
  and disables "Approve & send" (with "needs Senior reviewer") when the current
  role is below the required authority.

Touched: `Zynara.Core/Authority/*`, `Zynara.Core/Agents/IAgentRoster.cs`,
`Zynara.Core/Model/CaseRecord.cs`, `Zynara.Core/View/*`, `Zynara.Agents`
(`AgentRoster` + DI), `Zynara.ApiProxy/CaseFunctions.cs`,
`src/Zynara.Dashboard/standalone.html`, `Zynara.Core.Tests` (+9), TDD §7.

## D14 · "Region switch" → Policy + Regulatory Profile

**2026-09-06.** Evaluator P1-5 / §9. The UK ⇄ US control is reframed as a
**Policy + Regulatory Profile** — `RegulatoryProfile` in `Zynara.Core` carrying
the policy pack, terminology map, coding conventions, governing regulators,
appeal / escalation path, integration profile and currency per region. Built-in
`RegulatoryProfiles.Uk` / `.Us`; `config/profiles/*.json` is the deployment
override format (generated by `Zynara.DemoDump` so it can't drift);
`GET /api/profiles` + `/api/profiles/{region}`; dashboard shows it as a small
header panel behind the profile chip. **Kept low-key — not a product centrepiece**
(guardrail §18).
Touched: `Zynara.Core/Profiles/*`, `Zynara.ApiProxy/ProfileFunctions.cs`,
`tools/Zynara.DemoDump`, `config/profiles/`, `src/Zynara.Dashboard/standalone.html`,
`Zynara.Core.Tests` (+2), TDD §7.2.

## D13 · Document extraction is an upstream assumption, not in scope

**2026-09-06.** The pipeline starts *after* extraction. We assume, done by
processes outside this design: payer criteria already structured into the
versioned `Criteria` list (no policy-PDF → criteria ingestion agent — `PolicyDiff`
only diffs structured versions); the clinical note and denial letter already
plain text (no OCR / EHR-document parsing); precedent metadata and denial-history
cohorts already rolled up in Cosmos. What the pipeline owns is *reading* that
text against the structured criteria and grounding the agents in the corpus via
Foundry File Search. Documented in TDD §2.1; to be shown on the architecture
diagram as a shaded "upstream / not built" band.
Touched: TDD §2.1. **TODO: architecture SVG — add the upstream band.**

## D12 · Estimated Recoverable Value — the concept, with the working shown

**2026-09-06.** Evaluator P2-1 / §14. "Recovery £" is renamed **Estimated
Recoverable Value** and rebuilt so every assumption is visible:
`RecoveryEstimator` (pure) turns a `DenialCohort` (denied · appealed · won ·
not-appealed · mean claim value · window — an aggregate the system rolls up from
the case record) into a `RecoveryEstimate` where
`value = notAppealed × (won / appealed) × meanClaimValue`, always carrying that
formula string and a `RecoveryConfidence` (High/Medium/Low) set by the sample
size. `RecoveryService` returns the portfolio total + each payer/procedure scope;
`GET /api/recovery` serves it; the dashboard gets its own **Recoverable value**
tab (headline number, the four inputs, the arithmetic, confidence, the by-scope
table). No invented improvement figure (guardrail §18).
Touched: `Zynara.Core/Model/DenialCohort.cs`, `Zynara.Core/Recovery/*`,
`IZynaraStore` + `InMemoryZynaraStore`, Core DI, `DemoWorld` (2 cohorts),
`Zynara.ApiProxy/RecoveryFunctions.cs`, `tools/Zynara.DemoDump`,
`src/Zynara.Dashboard/standalone.html`, `Zynara.Core.Tests` (+6), TDD §11.

## D11 · Demo golden path + a visible Critic "catch"

**2026-09-06.** Evaluator P2-5 / §15. `docs/runbooks/demo-script.md` — a 3-minute
beat sheet built around the review's golden path, ending on the memorable line
("…challenged its own recommendation…"). Needed a scenario the earlier five
lacked: the Critic overturning a *plausible-looking* recommendation.

- New **`demo-appeal-critic`** scenario: a denial with two comparable cases that
  won on appeal (precedent-strategist drafts and recommends filing), but only one of
  three criteria is evidenced → Critic `Concerns` (*recommendation firmer than
  Low-quality evidence supports*) → Gate `HumanReview`.
- `CaseViewBuilder.Controls` now weighs the **Critic verdict + evidence quality**:
  when the Critic is uneasy or evidence is Low, *"Request more evidence"* becomes
  the primary action and *"Approve & send"* is de-emphasised — the reviewer is
  steered to fix the case, not sign off on it.
- Headline leads with the Critic's concern when it raised one.

Touched: `Zynara.Core/Demo/DemoCatalog.cs`, `Zynara.Core/View/CaseViewBuilder.cs`,
`demo-cases.json`, `Zynara.Core.Tests` (+2), `docs/runbooks/demo-script.md`.

## D10 · Reviewer read model + a self-contained experience API

**2026-09-06.** Evaluator P1-1 / P1-2. A pure projection —
`Zynara.Core.View.CaseView` — turns a `PipelineResult` into the evidence-first
reviewer card: WHY, per-criterion evidence (the quoted clinical statement +
source + policy clause + version), the top-3 precedent panel (similarity, won /
denied badges, matched facts, "drove the recommendation"), the Critic verdict,
the conflicts, the deterministic Gate with its decision model, the draft, and the
human-control set. `CaseService` runs → projects → stores; `ICaseRepository`
(in-memory now, Cosmos with `Zynara.Data`) holds the records.

`Zynara.ApiProxy` (.NET 8 isolated Functions) serves it: `GET /api/cases`,
`GET /api/cases/{id}`, `POST /api/cases`, `POST /api/cases/{id}/decision`,
`GET /api/demo/scenarios`, `POST /api/demo/scenarios/{id}/run`. It is
**self-contained for the demo** — seeds `DemoWorld` + pre-runs `DemoCatalog`, no
Cosmos, no orchestrator wiring — the same interim posture as `DemoWorld` in the
orchestrator. `Zynara.Dashboard` is one static `index.html`: offline from
`demo-cases.json` (generated by `tools/Zynara.DemoDump`), or live against the API
with `?api=<url>`.

Two supporting choices:
- `DemoWorld` gains a **second procedure with no precedent history** (MR arthrogram
  shoulder) so the abstain scenario is a genuine "no comparable history + thin
  evidence" case, not a tuning artefact.
- `AppealMatch.DeriveSupport` was **left unchanged** — a won-but-barely-comparable
  precedent still counts as Moderate support. Noted as a possible refinement
  (align with the Critic's 0.30 comparability threshold); deferred to avoid
  churning the Gate/eval tests now.

Touched: `Zynara.Core/View/*`, `Zynara.Core/Abstractions/ICaseRepository.cs`,
`Zynara.Core/Model/CaseRecord.cs`, `Zynara.Core/Demo/*`, `Zynara.Core` DI,
`src/Zynara.ApiProxy/*`, `src/Zynara.Dashboard/*`, `tools/Zynara.DemoDump/*`,
`Zynara.Core.Tests` (7 tests).

## D9 · Evaluator-plan decisions locked

**2026-09-06.** Answers to the open questions in
`../review/EVALUATOR-REVIEW-RESPONSE.md` §D:
- **Framing (§D-1):** keep the existing agent names (`needs-auth`, `evidence-gap`,
  `precedent-strategist`, `critic`) — no rename to "Analyst" roles. The multi-agent
  story is carried by the Critic + the "why multi-agent" section + the measured
  agents-vs-generalist comparison, not by role titles.
- **Sequencing (§D-3):** all P0 first, then the orchestrator — done.
- **Dataset (§D-4):** 8 labelled eval cases now; grow toward ~20 as the corpus
  and payer profiles land. Ground truth authored alongside each case.
- **Scope (§D-5):** target **P0 + all P1 + P2**. P1 next, demo-first order
  (P1-2 precedent panel + P1-1 evidence-first card → `Zynara.ApiProxy` read model
  + `Zynara.Dashboard`), then P1-5 / P1-3 / P1-4, then P2.

## D8 · Slice 4 — Durable orchestrator built; interim in-memory store

**2026-09-06.** `Zynara.Orchestrator` (.NET 8 isolated Functions) implements
TD-1 / TD-2 / TD-3: `POST /api/requests` Durable HTTP starter (keyed on request
id, idempotent), `AuthOrchestrator` hub sequencing five spoke Activity Functions
with a 3-attempt retry and owning the Gate inline, early-stop when auth is not
required. It is a step-for-step mirror of `Zynara.Core.Pipeline.AuthPipeline`,
which stays the tested single-process reference. Until `Zynara.Data` lands,
`DemoWorld` seeds an in-memory `IZynaraStore`. New Gate rule: an appeal draft
(`request.DenialLetter` present) never auto-submits — always HumanReview.
Touched: `src/Zynara.Orchestrator/*`, `tests/Zynara.Orchestrator.Tests/*`,
`Gate.cs`, `AuthPipeline.cs` (`DraftBuilder` → public), STATUS.

## D7 · Evaluation is a CI-gated metric suite, not a pass/fail script

**2026-09-06.** Evaluator P0-4. `eval/Zynara.Eval` runs the labelled case set
through the pipeline (deterministic stubs → repeatable) and scores: evidence
precision/recall, mandatory false-negative rate, precedent-citation accuracy,
hallucinated-reference count, Gate route agreement, safe-abstention rate,
unsafe-automation rate — plus a `GeneralistBaseline` straw man for the
agents-vs-one-generalist comparison. `dotnet test` **hard-gates** unsafe
automation = 0 and mandatory FN = 0.
Touched: `eval/Zynara.Eval/*`, `Zynara.sln`, TDD §7.3, STATUS.

## D6 · expiry-watch and policy-drift are deterministic services, not agents

**2026-09-06.** The independent review flagged that date arithmetic (expiry) and a
text diff (drift) do not need an LLM. `ExpiryMath` and `PolicyDiff` stay as
deterministic spokes with a **thin optional AI narrator** for the alert wording;
they are no longer counted among the reasoning agents. The reasoning agents are
now: needs-auth, evidence-gap, precedent-strategist, **critic**.
Touched: TDD §3/§6, ARCHITECTURE §5, the architecture SVG, STATUS.

## D5 · Add the Critic agent

**2026-09-06.** Evaluator P0-2. A second reasoning agent that tries to *disprove*
the assembled recommendation before the Gate — the thing that makes this
multi-agent rather than five specialised prompts. Seven checks (claim support,
clause support, precedent comparability, contradiction, mandatory criteria,
recommendation strength, abstention). Its verdict feeds the Gate: `Abstain` →
abstain, `Block` → human review even with perfect numbers, `Concerns` → cannot
auto-submit.
Touched: `Zynara.Core/Agents/Contracts.cs` (`ICriticAgent`, `CriticReview`),
`Zynara.Core/Pipeline/CriticCheck.cs`, `Gate.cs`, `Zynara.Agents/{Stubs,Foundry}`,
DI, provisioner, TDD §3/§5/§6, SVG.

## D4 · Structured decision model replaces the averaged readiness score

**2026-09-06.** Evaluator P0-3. A single `readiness = 6/7` number can average a
missing **mandatory** criterion away. Replaced with:
- `Criterion.Mandatory` — a hard gate, never averaged.
- `IEvidenceGapAgent` returns per-criterion findings
  (Documented / Partial / Missing / Contradicted) + the supporting clinical
  statement + an overall `EvidenceQuality`.
- The Gate weighs a `DecisionModel` and picks one of four routes:
  **AutoSubmit / Strengthen / HumanReview / Abstain**. Any unmet mandatory
  criterion or a contradiction → HumanReview (never resolved silently);
  Low evidence + Weak/None precedent support → **Abstain**.
Touched: `Zynara.Core/Model/{Domain,Results}.cs`, `Agents/Contracts.cs`,
`Pipeline/{EvidenceGapMatch,AppealMatch,AuthPipeline}.cs`, `Gating/Gate.cs`,
`Zynara.Agents/*`, TDD §3, ARCHITECTURE §8.

## D3 · Precedent matches carry their score and matched facts

**2026-09-06.** Groundwork for the evidence-first reviewer UI (evaluator P1-2).
`AppealMatch` now returns `PrecedentMatch(Precedent, Similarity, MatchedFacts)`
and derives a `PrecedentSupport` level (Strong / Moderate / Weak / None) the Gate
consumes.
Touched: `Agents/Contracts.cs`, `Pipeline/AppealMatch.cs`, `Model/Results.cs`.

## D2 · Agent layer built stub-first, Foundry behind the same interface

**2026-09-06.** Each agent is a `Zynara.Core` interface with a deterministic stub
twin (the default, used in CI) and a hosted Foundry implementation swapped in by
`ZYNARA_AGENTS=foundry`. Pattern carried from the prior project. Lets the whole
pipeline run in CI with zero live inference.
Touched: `Zynara.Core/Agents/*`, `Zynara.Agents/*`.

## D1 · No queue, Cosmos not SQL, no separate Intake

**2026-09-06.** See TDD §12 · TD-3 / TD-4 / TD-5. The Durable HTTP starter is the
async boundary; Cosmos DB (serverless) for operational state; Blob + Foundry
File Search for the unstructured corpus; `Zynara.Intake` project dropped (folded
into the orchestrator app).
Touched: architecture SVG/PNG, TDD §3–§12, ARCHITECTURE §8–§17, the repo tree.
