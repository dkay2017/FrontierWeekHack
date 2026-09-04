# Implementation decisions

Deltas from the TDD / Build Plan, agreed 2026-09-02. The TDD stays as the design of record;
this file is what we actually build. Guiding principle: **get one end-to-end run working first,
refine after.**

## D1 — One model: `gpt-5.4` for all three agents

The TDD specifies `gpt-4.1` (diagnosis) + `gpt-4.1-mini` (detection, work order). Challenge 0
deployed **`gpt-5.4`** — the only model available on this Foundry account (`factory-project`,
swedencentral, GlobalStandard).

**Decision:** all three agents (`IAgentClient` real impl) use the `gpt-5.4` deployment.
`MODEL_DEPLOYMENT_NAME=gpt-5.4` in `factory/.env`.

**Impact on the AI-governance story (D3):** "differentiated caps" become **per-agent** (via APIM
products / subscription keys), not per-model. Still a demonstrable, enforced control. The
per-model `gpt-4.1` vs `gpt-4.1-mini` split moves to the roadmap section.

## D2 — Durable Functions: pipeline runs inside one activity for v1

The Build Plan composes `run_pipeline = C→D→E→F→G→H→I` as one function. A C# Durable
**orchestrator** can't do IO directly (replay determinism), so the pure `Core.Pipeline` can't be
the orchestrator verbatim.

**Decision:** v1 — the queue-triggered orchestrator calls **one activity** that runs
`Core.Pipeline.Run(reading)` end to end (DB + `IAgentClient` calls inside the activity). Simple,
correct, gets us to end-to-end fastest.

**Refinement (later, if time):** split into step-level activities
(`ThresholdCheck` / `Detect` / `HistoryMatch` / `Diagnose` / `Act`) sequenced by the orchestrator —
this is the "hub & spokes" of the diagram and gives per-step retry + durability. `Core` logic is
identical either way; only the `TireForge.Orchestrator` wiring changes.

**✅ Implemented (session 4).** `TireForge.Orchestrator/PipelineFunctions.cs` —
`PipelineStarter` (queue trigger on `readings`, `InstanceId = reading.Id` so a
redelivered message is a no-op) → `PipelineOrchestrator` (deterministic, schedules
one activity) → `RunPipeline` (`[ActivityTrigger]`, injects `Pipeline`, returns
`PipelineRunSummary`). `TireForge.Ingestion` publishes the queue. See revised
sequence step 8.

## D3 — AI Governance / APIM AI Gateway: deliberately out of scope for the submission

**Decision (session 4):** the **APIM AI Gateway is not built.** It stays in the
design as the reference production control (TDD §7) — recognised as essential, not
overlooked — and moves to the roadmap.

**Why it's descoped:**
1. **The policies don't fit the hosted-agent path.** Since [D9] the agents are
   *hosted Foundry agents* invoked via the Responses API, not direct
   model-deployment calls. `azure-openai-token-limit` / `azure-openai-emit-token-metric`
   are written for the Chat Completions request/response shape, and the agent's
   real model calls happen **server-side** (the nested `chat gpt-5.4` spans APIM
   never sees). APIM in front of the agent endpoint can rate-limit by request
   count but cannot do the per-model **token** metering that is §7's whole point —
   without bespoke policy work.
2. **The visibility it would feed is available directly.** Per-agent token usage
   comes back in every agent response (`ResponseResult.Usage`) and in the App
   Insights `gen_ai` spans. Cost reporting does **not** depend on APIM — see [D13].
3. **Solo build inside the competition window.** The TDD's own build-priority list
   puts APIM at #3, below the working pipeline + reviewer loop + dashboard.

**What "governance" would add if scoped:** *enforcement* — a 429 when an agent
exceeds its token-per-minute budget — plus differentiated caps per agent. That is
the only capability lost by descoping; *cost visibility* is kept (D13).

**Roadmap shape (unchanged from TDD §7):** APIM Consumption, `azure-openai-token-limit`
+ `azure-openai-emit-token-metric` policies, differentiated caps, fronting either
the model deployment (for non-agent calls) or a custom policy that parses the
Responses `usage` shape.

> **⚠️ Superseded for the real-agent path by [D9].** The "direct model call" here is fine for a
> stub / spike, but Challenges 1–4 require **persistent Foundry agents** (portal-visible,
> agent-keyed traces / evaluations / workflows). Stage M creates real agents via the .NET
> Agents SDK; APIM (if kept) then sits in front of those agent calls.

## D4 — Data store: EF Core, SQLite now, swap provider if it bites

The domain is relational — 5 tables with FKs, joins for health metrics and `/queue`, a WorkOrder
lifecycle state machine. SQLite's real risk (concurrent writes on Flex/Consumption) is largely
mitigated by the **Work Order Adapter being the sole writer** (writes are serialized through one
code path).

**Decision:**
- `TireForge.Data` uses **EF Core** with repository interfaces (`IMachineStore`, `IReadingStore`,
  `IHistoryStore`, `IDiagnosisStore`, `IWorkOrderStore`).
- **Local / tests / Stages A–L / demo:** SQLite (file, or in-memory for tests). Zero infra,
  satisfies "no cloud" for the pure-logic stages. EF migrations = the Stage-A schema DDL.
- **Deploy target:** Azure SQL **serverless** (auto-pause, ~$0 idle) — provisioned by
  `infra/modules/data.bicep`, Entra-only auth, connection string wired to all three
  Function Apps as `TIREFORGE_DB`. Flex Consumption has no persistent local disk, so
  SQLite is local/tests/demo only. **Remaining before a live `azd up` persists:** add
  `Microsoft.EntityFrameworkCore.SqlServer`, make `AddTireForgeData` pick the provider
  from the connection string, generate a SqlServer migrations set (column types differ
  from SQLite), and an `azd postprovision` hook to migrate + seed.
- **Cosmos DB** stays an option if we later want JSON-native / globally-distributed, but it would
  mean hand-rolling the joins and FK integrity this design leans on — not the low-friction path
  for this data shape.

## D5 — Schema: 5 tables (Build Plan §15)

`Machines`, `Readings` (+ `is_anomaly`), `History`, `Diagnoses` (pending trace + gate reason),
`WorkOrders` (+ audit rows for rejects). Confirmed — `Diagnoses` is in, not the TDD's 4-table list.

---

## D6 — Correlation: W3C `Activity`, one trace per reading

The TDD's "one correlated trace ID per reading, across every hop" (invariant, §4) and
Challenge 2 (GenAI tracing) are the same requirement.

**Decision:** the pipeline's trace id is a real **W3C trace context** via
`System.Diagnostics.ActivitySource` (`TireForge.Pipeline`), not a bare GUID. One root
activity (`pipeline.run`) per `Pipeline.RunAsync`, one child activity per step
(`t1.threshold_check`, `a1.detect`, `t2.history_match`, `a2.diagnose`, `gate`,
`a3.draft`, `act`), each tagged with `tireforge.reading_id` / `machine_id` / `severity`
/ `confidence` / `gate.route`. `Diagnosis.TraceId` = the root's `TraceId` (32-hex), so
the stored id equals App Insights `operation_Id`.

**Export:** the Functions hosts already wire `Azure.Monitor.OpenTelemetry.Exporter`
(scaffold). Each `Program.cs` adds `.WithTracing(t => t.AddSource("TireForge.Pipeline"))`
so our spans reach App Insights (connection string from `factory/.env`). At Stage M the
Foundry SDK's `gen_ai.*` spans nest under our activity for free (also feeds the Cost tab).

**Complexity: low (~4/10)** — `ActivitySource` is BCL, exporter is scaffolded. Own stage,
right after the pipeline.

## D7 — Persist the work-order draft on every route (A1)

The reviewer drawer shows "Draft work order · prepared, not issued". The Review route
writes no `WorkOrders` row, so the drafted action text had nowhere to live.

**Decision:** add `Diagnosis.DraftActionText`; `WorkOrderWriter.ActAsync` records the A3
draft on the diagnosis on **both** routes (before the auto/review branch). Small: one
column + migration + one line.

## D8 — Dashboard scope + APIM timebox

- **Dashboard:** ✅ **ported (session 4)** — `src/TireForge.Dashboard/index.html`.
  The mock `api` object now `fetch`es `TireForge.ApiProxy` (`/status` `/queue`
  `/workorders` `/cost`); Approve / Reject / Close hit the reviewer POST endpoints.
  Machine roster + numbers come from `/status` (seeded Challenge data). `gpt-5.4`
  labels, mojibake fixed, sim trimmed + made explicitly illustrative (no client
  state mutation). `?api=` override; local CORS via `func start --cors "*"`. The
  v1.6 prototype stays in `docs/design/` as the design reference.
- **Dropped** (complexity without payoff for a judge):
  - Simulator "drift" / "stall" scenarios → cut the sim to `normal / warn / crit`
    (matches `ReadingFactory`; trend/rolling-window is TDD §9 roadmap).
  - Gateway 429-backoff event log as live narrative → keep as a static "what
    enforcement looks like" card.
  - `WorkOrderStatus.Draft` lifecycle state → the draft lives on the `Diagnosis` (D7).
- **APIM (D3):** timebox the feasibility spike to ~1 h. If APIM can't cleanly proxy the
  Foundry model path, **APIM moves to the roadmap** — keep the TDD governance section,
  keep the Cost tab as illustrative, cite App Insights GenAI token spans (Stage M) as
  the metering path.
- Cost tab shows real numbers only once Stage M agents emit token spans; until then it
  is labelled illustrative (invariant 1.5 — never present mocked figures as real).

## D9 — Real agents = persistent Foundry agents (not direct model calls)

Read of all challenge READMEs (session 3): Challenges 1–4 are **agent-keyed**, not just
"agent-shaped logic":

- **Ch 1** — `agents.create_version("anomaly-detection-agent", …)` → agents are hosted
  resources under **Build → Agents** in the portal.
- **Ch 2** — the Traces / Monitor / **Agents (preview)** views group by agent name and are
  populated by the Foundry service instrumenting **hosted-agent** calls (`gen_ai.*` spans,
  per-agent token consumption). A raw model call produces none of this.
- **Ch 3** — evaluation target is **"Agent"**; you pick `anomaly-detection-agent` from a
  dropdown and run Coherence / Fluency over `challenge-3-evaluate/eval_portal.jsonl`. The
  agent must exist as a Foundry resource. Portal-driven, not code.
- **Ch 4** — "both agents visible as persistent assets" + wire them in the portal **workflow
  designer**. (Azure Functions = production "Option 4" — our Durable orchestrator.)

**Decision:** Stage M's real `IAnomalyDetector` / `IFaultDiagnoser` / `IWorkOrderDrafter`
implementations **create persistent Foundry agents via the .NET Agents SDK** and invoke the
*hosted* agent — matching `factory/challenge-1-build/agents.py`:

| Agent name | Model | Tool | Source prompt |
|---|---|---|---|
| `anomaly-detection-agent` | `gpt-5.4` | `check_thresholds` function | agents.py system prompt |
| `fault-diagnosis-agent` | `gpt-5.4` | (none — prompt rubric) | agents.py system prompt |
| `work-order-agent` | `gpt-5.4` | (none) | our addition |

The `Core.Agents` interfaces and the whole pipeline / gate / adapter / reviewer chain are
**unchanged** — only the impls behind the interfaces change.

**Open spike — ✅ RESOLVED 2026-09-03 (session 4). Rung 0 works.** `tireforge/spikes/
FoundryAgentSpike` (pure C#, no Python) created `anomaly-detection-agent` v1 against
`factory-project`, invoked it once (correct output — 2 warn + 1 crit), confirmed it
portal-visible via REST, and the `invoke_agent anomaly-detection-agent:1` span
(`type: AI`) reached App Insights. Path = the **nextgen Foundry projects 2.x API**
(`Azure.AI.Projects` 2.0.1 + `Azure.AI.Projects.Agents` 2.0.0 + `Azure.AI.Extensions.OpenAI`
2.0.0), NOT the older `Azure.AI.Agents.Persistent`. `Azure.Core` 1.53 lets
`DefaultAzureCredential` pass straight in as `AuthenticationTokenProvider`. Full API
notes + code in `tireforge/spikes/FoundryAgentSpike/FINDINGS.md`.

**What this doesn't change:** Challenges 3 & 4 stay mostly **portal work** once the agents
exist (upload the jsonl + click through Evaluations; build the workflow in the designer).
`eval/TireForge.Eval` and the Durable orchestrator are the automated/production supersets
(Ch 4 "Option 5"), not replacements for the portal steps.

## D10 — `TireForge.ApiProxy` HTTP endpoints: anonymous auth for now

Stage L exposes the pure read models (`/status` `/queue` `/workorders` `/health`
`/cost`) plus the reviewer write path (`/review/approve` `/review/reject`
`/workorders/{id}/close`, over the Stage-K `Reviewer`) as HTTP-triggered Azure
Functions (isolated worker, ASP.NET Core integration).

**Decision:** every function uses `AuthorizationLevel.Anonymous` for now. The
dashboard (a static SPA port, D8) calls these directly with no key handling, and
the demo runs locally / in a Codespace with no gateway in front. Route prefix
stays the Functions default (`/api/...`).

**Why this is safe to defer:** the endpoints are read-only except the reviewer
decisions, which are already audited (`Rejected` rows, `by=reviewer`, invariant
1.1 — the Adapter is the sole writer). No secrets, no equipment control, no PII.

**When it changes:** if APIM goes in front (D3 spike passes), auth + rate limiting
move to the gateway and the Functions stay anonymous behind it (standard APIM ↔
Functions pattern). If APIM is dropped, add `AuthorizationLevel.Function` + a key
for any non-localhost deployment before `azd up`. Tracked in the roadmap, not v1.

## D11 — Agent SDK fallback ladder (provision vs. invoke are separable)

`factory/challenge-1-build/agents.py` uses the **nextgen** surface
(`client.agents.create_version` + `PromptAgentDefinition` + the Responses API with
`extra_body={"agent_reference": {…}}`). The .NET equivalent of *that exact
surface* may lag Python. But a Foundry agent is a **service-side resource**, not a
language object — once it exists in `factory-project`, anything that can send an
authenticated HTTPS request can invoke it, and the invocation API is
OpenAI-compatible (a POST; `agent_reference` is just an extra JSON field). Both
SDKs authenticate the same way (`DefaultAzureCredential` / `az login`). The
`Core.Agents` interfaces (`IAnomalyDetector` / `IFaultDiagnoser` /
`IWorkOrderDrafter`) already isolate the seam, so **how** an agent is created and
invoked is swappable without touching the pipeline / gate / adapter / reviewer.

**Feasibility: confirmed at every rung** (Challenge 4's README itself lists
"FastAPI app + client calls it" as a supported production pattern — Option 3).
The only variable is how much Python we tolerate.

**Preference order (agreed session 4):**

- **Rung 0 — ✅ CONFIRMED WORKING (session 4).** Pure C#, the **nextgen Foundry
  projects 2.x API** (`Azure.AI.Projects` + `Azure.AI.Projects.Agents` +
  `Azure.AI.Extensions.OpenAI`) — `AgentAdministrationClient.CreateAgentVersion` to
  provision, `ProjectResponsesClient.CreateResponse` (+ `AgentReference`) to invoke.
  Same API surface as `agents.py`, no Python. `DefaultAzureCredential` passes
  straight in (Azure.Core 1.53). Spike + full API notes:
  `tireforge/spikes/FoundryAgentSpike/`. **We build Stage M on this — the fallback
  below is now dead weight, kept only for the record.**

- **Fallback — `0 → 2 + 1`, fully scripted, no manual portal steps.** If rung 0's
  `CreateAgent` / invocation won't cooperate against `factory-project`:
  - **Rung 2** — a one-shot **Python deploy script** (`provision_agents.py`) using
    the same nextgen API as `agents.py`, run once like the Challenge-0 `deploy.sh`.
    It creates all three agents **end to end in code** — no clicking through the
    portal designer, no manual field config. Not in the request path; Python is
    infra tooling, like Bicep.
  - **Rung 1** — **all invocation in C#**, over the OpenAI-compatible Responses
    endpoint (`HttpClient` / `Azure.AI.OpenAI`, `agent_reference` in the body).
    This is what the pipeline's real `IAnomalyDetector` / `IFaultDiagnoser` /
    `IWorkOrderDrafter` call.
  - Net: C# **runtime** stays 100% C#; the only Python is a checked-in provisioning
    script that runs once per environment. **Zero manual configuration** — the
    portal is used for *viewing* (Traces / Monitor / Evaluations / Workflows), not
    for *setup*.

- **Rung 3 — last resort only.** Python **sidecar** (FastAPI) owns create + invoke;
  C# `HttpAnomalyDetector : IAnomalyDetector` POSTs to it. The only rung that
  dilutes "everything C#" and adds an always-on service to deploy. Design already
  supports it; stays documented but unused unless C# genuinely cannot invoke a
  hosted agent at all.

## D12 — Real agents: hybrid split — agent writes the prose, Core owns the numbers

The `Core.Agents` structured outputs (`AnomalyVerdict.IsAnomaly`,
`FaultVerdict.Severity` / `Confidence` / `Cites`, every `WorkOrderDraft` field bar
`ActionText`) drive the Gate and the write path — invariants 1.3 / 1.1. Letting a
model produce them means a hallucinated confidence can auto-issue a work order, and
the Gate stops being unit-testable.

**Decision:** the Stage-M real implementations are **hybrid**:

| Field | Source | Why |
|---|---|---|
| `AnomalyVerdict.IsAnomaly` | deterministic — `t1.AnyBreach` | T1 is agent-independent + unit-tested (design); the gate on "is there an anomaly" must not flake |
| `AnomalyVerdict.Text` | **the agent** (calls `check_thresholds`, emoji-tagged summary) | Challenge 1's actual deliverable — coherent grounded narrative |
| `FaultVerdict.Fault` | **the agent** (LIKELY CAUSE line) | the reasoning Challenge 1 asks for |
| `FaultVerdict.Text` | **the agent** (LIKELY CAUSE / ACTIONS / URGENCY) | what Challenge 3 scores for Coherence / Fluency |
| `FaultVerdict.Severity` | deterministic — `FaultHeuristics.Escalate(t1, t2)` | drives the Gate (invariant 1.3) |
| `FaultVerdict.Confidence` | deterministic — `FaultHeuristics.Score(t1, t2)` | drives the Gate; a model's self-reported confidence is not calibrated |
| `FaultVerdict.Cites` | deterministic — reading id + `t2` incident ids | grounding must be real record ids, not model output |
| `WorkOrderDraft.ActionText` | **the agent** (work-order-agent) | the human-readable instruction the reviewer sees |
| every other `WorkOrderDraft` field | deterministic — copied from the `Diagnosis` | already decided upstream |

So the agents own exactly the free-text fields; deterministic Core owns everything
structural. `FaultHeuristics` (`Escalate` + `Score`) is lifted out of
`StubFaultDiagnoser` into `Core/Agents` so the stub and the Foundry impl share one
copy. This matches Challenge 1 (its Fault Diagnosis agent is pure narrative — no
structured fields at all) and keeps our superset (Gate / confidence / citations)
deterministic and testable.

**Consequence:** stub vs. Foundry produce the *same* gate routing for a given
reading; they differ in the prose (and the `Fault` label). That's intended — the
agent's value is the reasoning quality (Challenge 1 & 3), not the control flow.

**DI switch:** `AddTireForgeAgents(config)` reads `TIREFORGE_AGENTS` —
`stub` (default; offline, tests) or `foundry` (real, needs `az login` + `factory/.env`).

**✅ Implemented (session 4).** `src/TireForge.Agents/Foundry/` — `FoundryAgentClient`
(ensure-agent + invoke with the function-tool loop), `ThresholdsTool` (`check_thresholds`,
body → `Core.ThresholdCheck`), the three `Foundry*` impls, `FoundryAgentProvisioner`,
`AgentPrompts`. `tools/TireForge.AgentTool` provisions all three + runs one full
pipeline pass. Verified live: the CP-003 critical reading produced a correct A1
(grounded, tool called twice), A2 (LIKELY CAUSE / ACTIONS / URGENCY, cites inc-005/006),
A3 (IMMEDIATE, cites the reading); Gate → Review (deterministic). App Insights showed
one `pipeline.run` trace with `invoke_agent anomaly-detection-agent:2` / `fault-diagnosis-agent:1`
/ `work-order-agent:1` + `chat gpt-5.4-2026-03-05` spans nested per step = **Challenge 2
for real**; the three agents are portal-visible = **Challenge 1 for real**.
120 tests green.

## D13 — Agent cost metering: from the agent responses, not APIM

Cost **visibility** and AI **governance** ([D3]) are separate concerns. Descoping
APIM removes *enforcement* (429 on quota); it does **not** remove cost reporting.

**Decision (session 4):** every agent invocation's token usage
(`ResponseResult.Usage` — input / output / total, already captured in
`FoundryAgentClient.AgentInvocation`) is persisted to an **`AgentCalls` metering
table** in `TireForge.Data` via an `IAgentCallRecorder` port
(`Core.Agents` interface, `Data` impl):

| Column | |
|---|---|
| `Id` / `At` / `TraceId` / `ReadingId` | correlation |
| `AgentName` / `Model` | `anomaly-detection-agent` … / `gpt-5.4` |
| `PromptTokens` / `CompletionTokens` | summed across a tool-loop invocation |

- The three `Foundry*` impls record a row per `InvokeAsync`. **Stub agents record
  nothing** → the table is empty under `TIREFORGE_AGENTS=stub`, and the Cost tab
  shows "—" (honest — matches D8's "never present mocked figures as real").
- `Reports.CostAsync` aggregates `AgentCalls` by agent, applies a static `gpt-5.4`
  price table ($/1M in, $/1M out), and returns real `tokens` + `spend`.
  `TokenMetricsAvailable` flips true once any row has tokens.
- The dashboard Cost tab already renders real numbers when `TokenMetricsAvailable`
  is true (no UI change).

**This is the metering path §7 referred to** ("reconciled against the metering
table") — it just doesn't run through APIM.

## D14 — Portal workflow = 2 agents; the 3rd + the gate live in the orchestrator

The Foundry portal workflow designer (Challenge 4 Part 2) builds a **linear,
unconditional** agent chain. Our `factory-health-workflow-portal` is therefore
**`anomaly-detection-agent → fault-diagnosis-agent → End`** — two agents, matching
Challenge 4's own reference flow (anomaly scan → fault diagnosis → report).

**`work-order-agent` is deliberately not a node in the portal workflow.** Reasons:

1. **Semantics.** A linear 3rd node would draft a work order for *every* machine,
   including healthy ones — contradicting invariant 1.3 (work orders exist only
   *after* the Gate: `confidence ≥ 0.70 AND severity ≠ Crit` → auto, else review).
   The portal designer cannot express that condition.
2. **The 3rd agent's value is its *conditional* invocation.** Showing it ungated
   in the portal removes the one thing that makes it a superset delta.
3. **No added value.** A 3-node portal chain demonstrates nothing the 2-node chain
   + the code orchestrator doesn't already, and introduces an inconsistency a
   reviewer would (rightly) question.

**Where the rest lives:** `Core.Pipeline` + `Core/Gating/Gate` + `WorkOrderWriter`
(sole-writer Adapter) + `Core/Reviewing/Reviewer`, run by the Durable Functions
orchestrator (`TireForge.Orchestrator`). The orchestrator calls `work-order-agent`
**only on the route the Gate permits**. The portal workflow captures ~30 % of the
design (the agent handoff); the gate / adapter / reviewer / idempotency / tracing
are inherently code.

**Submission framing:** *"Portal workflow = the visual 2-agent demo. The Durable
orchestrator = the production system — it adds the confidence gate, the sole-writer
adapter, the human review loop, and invokes the 3rd agent only when a work order
is warranted."*

## D15 — Dashboard host = Static Web App **Standard** (linked backend), not Free

**Context.** The dashboard (`src/TireForge.Dashboard/index.html`) is a static page;
the data comes from `TireForge.ApiProxy`, a separate Function App on its own
`*.azurewebsites.net` origin. On **SWA Free** the two cannot be joined: Free tier
has no *linked backend*, so the page reaches the API **cross-origin** — the API
URL is passed in as a `?api=…` querystring and `TireForge.ApiProxy` runs CORS `*`.
It works, but it's a bolt-on: the URL carries deployment detail, and the API is
open to any origin.

**Considered and rejected — storage `$web` static website.** Storage static-site
hosting *also* has no backend-linking; we'd keep the identical `?api=` + CORS `*`
setup, just served from `*.web.core.windows.net`, and would need a bespoke
`az storage blob upload-batch` deploy step (azd has no host type for it). No gain.

**Decision.** Move the SWA to **Standard** (`sku: { name: 'Standard' }`, ~$9/mo)
and add a **`Microsoft.Web/staticSites/linkedBackends`** resource pointing at
`tireforge-apiproxy`. The SWA then serves the API at **`/api/*` on its own
origin** — same-origin calls, no CORS, no querystring. The dashboard's
`API_BASE` becomes the default same-origin `/api`; `?api=` stays as a
local-dev override only. `TireForge.ApiProxy` CORS can be dropped (or left as a
harmless fallback).

**Why pay.** The Free tier's one hard limitation was exactly this seam; $9/mo
removes it cleanly rather than shipping the querystring/CORS workaround in the
submission. Everything else in the stack is already consumption-billed.

**Status:** decided end of session 4 (2026-09-03); implemented session 5 — SWA
`sku: Standard` + a `Microsoft.Web/staticSites/linkedBackends` → the apiproxy.
`staticWebAppSku` param (azd `STATIC_WEB_APP_SKU`, default `Standard`) toggles back
to `Free` if needed. Dashboard `API_BASE` already defaults to same-origin `/api`.

## D16 — Secrets & identity: managed identity first, Key Vault for the residual

**Stance.** No secret in app configuration that managed identity can replace.

| Path | Auth | Secret? |
|---|---|---|
| Azure SQL | connection string `Authentication=Active Directory Default` → each Function App's system-assigned MI; `DbDeploy` grants `db_datareader`/`db_datawriter` | none |
| Foundry (agent calls) | `DefaultAzureCredential` → orchestrator MI; **Cognitive Services User** on the account | none |
| Functions runtime storage (`AzureWebJobsStorage`) — host, Durable hub, `readings` queue | **identity-based** (`AzureWebJobsStorage__accountName` + `__blob/queue/tableServiceUri`); MI gets **Storage Blob Data Owner** + **Queue Data Contributor** + **Table Data Contributor** | none |
| App Insights | connection string, but delivered as a **`@Microsoft.KeyVault` reference** | vaulted |
| Deployment content share (`WEBSITE_CONTENTAZUREFILECONNECTIONSTRING`) | storage **key** — *unavoidable on Consumption/Y1*, only Flex removed it — delivered as a **`@Microsoft.KeyVault` reference** | vaulted (platform-forced) |

**Key Vault** (`modules/keyvault.bicep`) — RBAC-authorization mode (no access
policies), soft-delete 7 days, purge protection off (hackathon). Two secrets:
`storage-connection-string`, `appinsights-connection-string`. Each Function App MI
gets **Key Vault Secrets User**; `keyVaultReferenceIdentity: SystemAssigned`.
App settings carry `@Microsoft.KeyVault(SecretUri=…)` (unversioned → auto-rotate),
never the literal.

**Toggles** (azd env) for a fast rollback if a platform edge bites:
`STORAGE_IDENTITY_BASED` (default true), `CONTENT_SHARE_KEY_IN_VAULT` (default true;
staged — first provision inline, flip once the KV role has propagated so the
cold-start file-share mount can't race the role assignment).

**Not done (noted):** App Insights could be fully identity-based
(`APPLICATIONINSIGHTS_AUTHENTICATION_STRING=Authorization=AAD` + *Monitoring
Metrics Publisher*) — deferred; the ingestion key is low-sensitivity and now
vaulted. SQL firewall is `AllowAllAzureIps` (0.0.0.0) — fine for MI-only auth;
Private Endpoint is the production hardening.

**Doc owed:** a **"Security"** section in the TDD — see `PENDING-DOC-UPDATES.md` §6.

## Revised build sequence (post-Stage J)

1. ✅ **A1** — `Diagnosis.DraftActionText` (D7).
2. ✅ **Tracing stage** — `Activity`-based correlation + host export (D6) = Challenge 2 (our side).
3. ✅ **Stage K** — reviewer approve / reject / close.
4. ✅ **Stage L** — read models (`Core/Reporting`) + `TireForge.ApiProxy` HTTP endpoints
   (5 read + 3 reviewer-write, anonymous auth per D10, `ApiJson` wire shape, `HttpProblem`
   error mapping). 12 ApiProxy tests. Live `func` host smoke test pending (no core-tools
   in this Codespace) — deferred to the dashboard-port step.
5. ✅ **Stage M spike (D9)** — done (session 4). `tireforge/spikes/FoundryAgentSpike`:
   pure C# created `anomaly-detection-agent` v1, invoked it (2 warn + 1 crit),
   portal-visible, `invoke_agent` span in App Insights. Rung 0 of the D11 ladder
   confirmed; fallback not needed. API notes in the spike's `FINDINGS.md`.
6. ✅ **Stage M full** (session 4) — `src/TireForge.Agents/Foundry/` + `tools/TireForge.AgentTool`.
   Three agents provisioned + wired behind the `Core.Agents` interfaces (hybrid per D12);
   `TIREFORGE_AGENTS=stub|foundry` switch. Live full-pipeline pass verified;
   agent spans nest under the `pipeline.run` trace in App Insights = **Challenges 1 & 2
   for real**. 120 tests green.
7. ✅ **Dashboard port** — `src/TireForge.Dashboard/index.html` (port of prototype
   v1.6). Mock `api` → real `fetch` at `TireForge.ApiProxy` (`/status` `/queue`
   `/workorders` `/cost`; Approve/Reject/Close → the reviewer POSTs). Machine
   identity from `/status` (seeded data, not the prototype roster). `gpt-5.4`
   labels; mojibake fixed; sim trimmed to normal/warn/crit and made explicitly
   illustrative (no client-side state mutation); Cost shows call counts + "—" for
   token/spend until the gateway (D8). `?api=` override; CORS via
   `func start --cors "*"` (`local.settings.sample.json`). jsdom render smoke test
   green. Live `func` host smoke test still pending (no core-tools in this Codespace).
8. ◐ **Ingestion + Orchestrator + infra** (session 4):
   - ✅ **code** — `TireForge.Ingestion` (`SensorSimulator` timer + `EmitReading`
     HTTP → `readings` queue) + `TireForge.Orchestrator` (`PipelineStarter` queue
     trigger → `PipelineOrchestrator` → `RunPipeline` activity = one
     `Core.Pipeline.RunAsync`, D2; instance id = reading id for idempotency). Both
     hosts wire `AddTireForgeData` + `AddTireForgeAgents`. 4 `Orchestrator.Tests`.
   - ✅ **infra** — `infra/modules/apps.bicep` (identity-only storage + Durable hub
     + `readings` queue, Flex Consumption plan, 3 Function Apps with MI + App
     Insights + RBAC, Free SWA for the dashboard) + `infra/modules/data.bicep`
     (Azure SQL serverless, D4). `az bicep build` clean; `azure.yaml` maps all 4
     services.
   - ⬜ **remaining for Challenge 4:** (a) EF `SqlServer` provider + a SqlServer
     migrations set + `azd postprovision` to migrate/seed (see `infra/README.md`);
     (b) live `azd up`; (c) the portal workflow-designer steps.
9. ◐ **Challenge 3** — ✅ `eval/TireForge.Eval` (CI-gate, 10/10 classification) +
   `.github/workflows/tireforge-ci.yml` + `docs/runbooks/challenge-3-portal-evaluation.md`;
   ⬜ the manual portal Coherence/Fluency run.
10. **Agent cost metering (D13)** — `AgentCalls` table + `IAgentCallRecorder`;
    `Reports.CostAsync` → real tokens + spend. Replaces the old "APIM step":
    **APIM itself is descoped (D3)** — governance = documented roadmap.
