# TireForge — build status / session context

**Purpose:** rehydrate context fast after a Codespace or Claude-session restart.
Keep this file current at every checkpoint and **commit + push it** — a Codespace
rebuild loses anything uncommitted (see the `codespace-data-loss` memory).

_Last updated: 2026-09-05. **⚠ Possible pivot under consideration** — see
`PIVOT-PRIOR-AUTH.md`. Decided in principle: a new multi-agent concept (Prior
Authorization Copilot, healthcare) scored higher on Innovation/Impact than
Meridian's ceiling allows, after rejecting 4 other candidates. Not yet scoped
or built. Meridian would become the completed PoC; the new idea likely gets
its **own repo + resource group**. Deadline 2026-09-24. **Resume point: read
`PIVOT-PRIOR-AUTH.md` first**, then decide with the user whether to proceed,
before touching either codebase._

_Below this line is Meridian/TireForge's own history, current as of session 6
(2026-09-04): **T0 predictive early warning is LIVE** — provisioned +
redeployed + verified end-to-end on real sensor traffic. Its own remaining
items (re-run Ch3 eval, 4th agent fast-follow, doc pass, judging-score
backlog) are unblocked but paused pending the pivot decision above._

### Session 6 — done

1. ✅ **Re-provisioned Foundry** — `AgentTool provision` pushed the Meridian
   rebrand live: `anomaly-detection-agent:3`, `fault-diagnosis-agent:2`,
   `work-order-agent:2`.
2. ✅ **Redeployed all 3 Function Apps** (ingestion/orchestrator/apiproxy), one
   at a time — T0 wiring + `GET /api/warnings` now live.
3. ✅ **Applied the `AddEarlyWarnings` migration to Azure SQL** — this was
   missing and caused the first live check to 500 (`EarlyWarnings` table didn't
   exist yet); `TireForge.DbDeploy` run fixed it, idempotent, no data loss.
4. ✅ **Verified T0 live, twice over:**
   - A throwaway verification script (`Core.Pipeline.RunAsync` called directly
     against the live Azure SQL DB, stub agents, deleted after) confirmed the
     DB-write + read-path plumbing end to end.
   - **Better proof: the orchestrator had already organically raised 2 real
     early warnings on its own** (IS-005 vibration, MX-001 pressure) from
     nothing but live sensor-sim traffic, before the verification script even
     ran — visible at `GET /api/warnings` and the dashboard's Early Warnings tab.
     Synthetic test rows cleaned up afterward; the 2 organic ones are real.

### Session 6 — remaining

1. **Re-run the Challenge 3 portal eval** if there's time — the anomaly-detection
   prompt changed (company name only; Coherence/Fluency shouldn't move, but the
   100/100 was scored against the prior version, now `:3`).
2. **Fast-follow, not urgent:** the `predictive-maintenance-agent` (4th Foundry
   agent, narrates the T0 warning instead of the deterministic sentence) —
   deliberately deferred, see DECISIONS **D17**. The feature works without it.
3. The consolidated doc-reconciliation pass (item 4 below) + the rest of the
   judging-score backlog (item 5 below) + re-score the judging self-assessment
   now that D17 is actually live, not just tested.

### Session 5 — punch list

1. ✅ **Sensor timer re-enabled** (`AzureWebJobs.SensorSimulator.Disabled` deleted).
2. ✅ **D15 + D16 — SWA Standard + Key Vault + identity-based storage.** New
   `infra/modules/storage.bicep` + `keyvault.bicep`; `apps.bicep` rewritten.
   - SWA Free → **Standard** + `linkedBackends` → apiproxy. Bare dashboard URL
     now serves `/api` same-origin (no `?api=`, no CORS).
   - `AzureWebJobsStorage` → **managed identity** (`__accountName` + service URIs);
     per-identity Storage Blob Data Owner + Queue/Table Data Contributor.
   - Key Vault `tfkvcy3oncsu6rsla` (RBAC mode). `APPLICATIONINSIGHTS_CONNECTION_STRING`
     → `@Microsoft.KeyVault` ref; each identity → Key Vault Secrets User.
   - **Staged rollout complete:** provision 1 (`CONTENT_SHARE_KEY_IN_VAULT=false`)
     → verify → provision 2 (`=true`) → restart → cold-start with the content-share
     string from KV (`WEBSITE_SKIP_CONTENTSHARE_VALIDATION=1`) verified: all 3 apps
     up, emit→queue→orchestrator→agents→gate chain green, telemetry flowing.
   - **Net: zero plaintext secrets in app config** — both KV secrets are
     `@Microsoft.KeyVault` references. SQL/Foundry/storage-runtime = MI.
   - Rollback toggles (azd env): `STORAGE_IDENTITY_BASED` /
     `CONTENT_SHARE_KEY_IN_VAULT` / `STATIC_WEB_APP_SKU`.
3. ✅ **Orchestrator telemetry — resolved.** After the session-5 re-provision +
   restart, App Insights shows the full trace tree from the orchestrator:
   `PipelineStarter → RunPipeline → pipeline.run → t1/a1/t2/a2/gate/a3/act` +
   `invoke_agent anomaly-detection-agent:2 / fault-diagnosis-agent:1 /
   work-order-agent:1` + `chat gpt-5.4-2026-03-05`, all `success`. Yesterday's
   empty telemetry was Y1 de-allocating the cold instance before the OTel batch
   exporter flushed (single manual emit); the sensor sim + steady traffic keeps
   it warm now. If it regresses under true idle, shorten the OTel batch delay or
   add `WEBSITE_...` always-ready — noted, not needed.
4. **Consolidated doc-reconciliation pass** — `PENDING-DOC-UPDATES.md` §1–§6
   (SQLite→Azure SQL · Challenge 3 eval + CI · APIM descoped + cost metering ·
   Flex→Y1 + suffix · Challenges 3 & 4 portal · **§6 security + new TDD "Security"
   section**). Design docs / TDD / README / architecture SVG in one sweep.
5. **Improve the judging score** — see `JUDGING-SELF-ASSESSMENT.md`. Strict
   self-assessment (2026-09-04): **50/90** (Innovation 16, Usability 17, Impact
   17 — every score mid-Medium, no standout). Backlog, cheapest-first: cold-start
   progress indicator (Usability) → foreground the hybrid-agent/gate design +
   portal-workflow reasoning in the write-up (Innovation) → an illustrative ROI
   figure on the Health Report (Impact) → light auth on reviewer writes → warm-keep
   before a judge session → **one genuinely new capability beyond the reference**
   (the only lever that changes Innovation's *band*, not just its score).

_**Challenges 0–4 all complete:** 0 infra ✅ · 1 agents (Stage M) ✅ · 2 App
Insights agent traces ✅ · 3 portal eval **100/100** ✅ · 4 portal workflow
`factory-health-workflow-portal` (2 agents, D14) + Durable orchestrator ✅._

_**Code done:** Stages A–M · dashboard · Ingestion/Orchestrator · Challenge-4 infra
(bicep) · runtime **SQLite→Azure SQL** (tests = in-memory SQLite double) · **APIM
descoped** (D3) · **cost metering** (D13) · `eval/TireForge.Eval` CI gate (10/10) ·
CI workflow · **`tools/TireForge.DbDeploy`** (azd postprovision — migrate + grant
Function App identities + seed) + `azure.yaml` `hooks.postprovision`._

_**Live deploy — DONE + VERIFIED END TO END (session 5).** RG
`foundry-hackathon-rg-3e97ae19`, **classic Consumption (Y1) Linux**. Function Apps
carry a `-v2` suffix (`FUNCTION_APP_SUFFIX` — the `tf1` names got soft-deleted).
**Identity-first (D16):** SQL + Foundry + `AzureWebJobsStorage` all managed
identity; Key Vault `tfkvcy3oncsu6rsla` holds the App Insights + content-share
strings as `@Microsoft.KeyVault` refs. **D15:** SWA **Standard** + `linkedBackends`
→ apiproxy; the dashboard serves `/api` same-origin — and the linking auto-enabled
EasyAuth on the apiproxy so it is **only reachable through the SWA** (direct
`*.azurewebsites.net` = 401 — a bonus lockdown). **Proven live (session 5):**
`POST` to ingestion `/api/emit/CP-003/crit` → `readings` queue (MI) → Durable
orchestrator (MI) → 3 Foundry agents → Gate → review row via `SWA/api/queue`.
App Insights shows the full `pipeline.run` tree + `invoke_agent <name>` + `chat
gpt-5.4` spans from the orchestrator (**#3 telemetry now flowing** — yesterday's
gap was Y1 killing the cold instance before the OTel batch flushed). `SWA/api/cost`
= real token metrics (**D13 live**)._

_**Live URLs:**_
- _Dashboard (+ API, same-origin): `https://jolly-glacier-02859e803.3.azurestaticapps.net`_
  _— bare URL now works; `?api=` no longer needed (and direct apiproxy is 401 by design)._
- _Ingestion (direct, not linked): `https://tireforge-ingestion-tf1-v2.azurewebsites.net/api/emit/{machine}/{mode}`_
- _Key Vault: `tfkvcy3oncsu6rsla` · verify flow uses `SWA/api/*` for reads._

_**136 tests green** (~5 s, no Docker; +8 for T0 TrendCheck, session 5).
CI green. All committed + pushed. Not yet deployed — see the session 6 punch
list at the top._

---

## What we're building

**TireForge Anomaly-Fault IQ** — predictive-maintenance multi-agent system for the
MS Agent-a-Thon **Factory** scenario, Architect Track (Deepak Kumar, original work).
Implemented in **C#/.NET 8** (upstream ships Python only).

Sensor telemetry → anomaly detection → fault diagnosis → work-order decision →
human review gate → health report. Foundry Agent Service + Durable Functions +
APIM AI Gateway.

## Source-of-truth documents

| Doc | Path |
|---|---|
| TDD (design of record) | `tireforge/docs/design/TireForge-Anomaly-Fault-IQ-TDD.md` |
| Build Plan (Stages A–M) | `tireforge/docs/design/TireForge-Anomaly-Fault-IQ-Build_Plan.html` |
| Implementation deltas | `tireforge/docs/design/DECISIONS.md` |
| Design→project map | `tireforge/docs/design/README.md` |
| Architecture SVG | `tireforge/docs/design/TireForge-Anomaly-Fault-IQ-Architecture_Design.svg` |
| **Judging self-assessment + improvement backlog** | `tireforge/docs/progress/JUDGING-SELF-ASSESSMENT.md` |
| **Course "Final Activity" brief + 5-point mapping** | `tireforge/docs/progress/COURSE-FINAL-ACTIVITY.md` |

## Working method (agreed session 3)

The `factory/challenge-*/` folders are the **conceptual guide + acceptance bar**,
not the implementation. We build the **C# equivalent** in `tireforge/`, using each
challenge README as the requirements doc. The Python files (`agents.py` etc.) are
reference only — never run for the submission. Challenge → stage map:

**All challenges 1–4 are agent-keyed — they need real *persistent Foundry agents*
(portal-visible, created via `agents.create_version` / the .NET Agents SDK), not
just "agent-shaped logic". See DECISIONS.md D9.**

| Challenge | Acceptance bar | tireforge work | State |
|---|---|---|---|
| 0 | Foundry infra + model playground works | deployed + `infra/main.bicep` | ✅ done |
| 1 | `anomaly-detection-agent` + `fault-diagnosis-agent` created via SDK, flag 2 warn + 1 crit | **all 3 agents live** (`anomaly-detection-agent` + `check_thresholds` tool, `fault-diagnosis-agent`, `work-order-agent`), pure C#, wired behind `Core.Agents` interfaces, full pipeline pass verified | ✅ done (Stage M full) |
| 2 | agent-keyed traces in portal Traces/Monitor/Agents(preview) | one `pipeline.run` trace in App Insights with `invoke_agent <name>:<ver>` + `chat gpt-5.4-2026-03-05` spans nested per step; tool loop visible | ✅ done (Stage M full) |
| 3 | evaluate the `anomaly-detection-agent` target in the portal (Coherence/Fluency over `eval_portal.jsonl`) | agent now exists — portal Evaluations run + `eval/TireForge.Eval` CI-gate superset outstanding | ⬜ unblocked, not started |
| 4 | agents visible as persistent assets + portal workflow designer | 3 agents live ✅; Durable pipeline wiring ✅; `infra/modules/apps.bicep` + `data.bicep` (Flex Consumption ×3 + storage + Azure SQL + SWA + RBAC) ✅ compiles; outstanding: EF SqlServer swap → live `azd up` → portal workflow designer | 🟡 code + infra done, deploy + portal pending |
| superset | APIM gateway · Dashboard · Reviewer gate · Work Order Adapter | Reviewer ✅, read models ✅, ApiProxy ✅, dashboard port ✅; APIM last (D3 spike gated) | 🟡 APIM only |

**Challenge 1 specifics (from its README + `agents.py` + `sensor_data.json`):**
- Seed data = `factory/challenge-1-build/sensor_data.json` — 5 machines, each with
  `machine_id`, `name`, `description`, `status`, `last_maintenance`, `readings`
  (temp/pressure/vibration/rpm → value+unit), `thresholds` (min/max per sensor).
  Expected: MX-001 mixer = warning, IS-005 inspection = warning, CP-003 curing_press = critical.
- `check_thresholds` tool == our Stage C ThresholdCheck (T1): in-spec test +
  deviation `% above max` / `% below min`. Port this logic near-verbatim.
- Anomaly agent: has the threshold tool, emoji-tagged structured summary.
- Fault Diagnosis agent: **no tools**, prompt-only fault rubric (temp+pressure→blockage,
  vib-alone→bearing wear, temp+vib→bearing/lube, multi-crit→compound). Output:
  `LIKELY CAUSE / MAINTENANCE ACTIONS / URGENCY`. Our design adds HistoryMatch +
  confidence + citations on top (superset).

**Where our design goes beyond Challenge 1 (superset deltas — keep explicit):**

| Aspect | Challenge 1 (workshop) | TireForge design |
|---|---|---|
| Agent count | **2** — Anomaly Detection, Fault Diagnosis | **3** — + **Work Order agent** (drafts the WO, cites the reading; Stages H–I) |
| Grounding for diagnosis | Foundry **knowledge base / File Search** over maintenance manuals, historical incident reports, supplier spec sheets (suggested on the Ch1 slide, not wired in `agents.py`) | Deterministic **HistoryMatch (T2)** — SQLite `History` table, fault-signature lookup, returns cited `inc-…` ids. Testable, no vector store. |
| Post-diagnosis flow | ends at a recommendation printed to terminal | **Gate** (confidence/severity) → **Work Order Adapter** (sole write path) → **Reviewer** loop |
| Threshold logic | `check_thresholds` tool inside the agent | pure `Core` **ThresholdCheck (T1)**, agent-independent, unit-tested |

Open option (roadmap, not v1): add a Foundry File Search knowledge base
(manuals / incident reports / supplier specs) as an *extra* grounding tool for the
Fault Diagnosis agent, alongside the deterministic HistoryMatch. Deterministic
T2 stays the primary path (citeable, testable); File Search would be the "richer
context" layer if time allows.

## Key rules (from Build Plan §1 invariants)

- Work Order Adapter is the **only** write path.
- Every agent verdict cites its source record (`rdg-…` / `inc-…`).
- Gate: `confidence < 0.70` OR `severity == "crit"` → human review. Exactly `0.70` → auto.
- Agents may only read + write WorkOrders. Nothing touches equipment.
- Cost figures come from real APIM token metrics, never mocked.
- Health Report is a dashboard tab, not an agent.

## Implementation decisions (see DECISIONS.md for detail)

- **D1** all 3 agents use `gpt-5.4` (only model on the Foundry account); APIM caps become per-agent.
- **D2** v1 Durable orchestrator = ONE activity running `Core.Pipeline.Run` end-to-end.
  ✅ implemented — `TireForge.Orchestrator/PipelineFunctions.cs` (queue → starter →
  orchestrator → `RunPipeline` activity), `TireForge.Ingestion` publishes the queue.
- **D3** APIM built LAST. *(For the real-agent path, superseded by D9 — agents are
  hosted Foundry agents, not direct model calls; APIM would front those.)*
- **D9** Stage M real agents = **persistent Foundry agents**
  (`anomaly-detection-agent` / `fault-diagnosis-agent` / `work-order-agent`,
  portal-visible), because Challenges 1–4 are agent-keyed. Interfaces + pipeline
  unchanged. Spike brought forward.
- **D10** `TireForge.ApiProxy` endpoints — `AuthorizationLevel.Anonymous` for now
  (keyless dashboard SPA; gateway or Function key before any non-local deploy).
- **D12** Real agents = **hybrid** (session 4): the agent writes the prose
  (`AnomalyVerdict.Text`, `FaultVerdict.Fault`+`Text`, `WorkOrderDraft.ActionText`);
  deterministic Core owns everything that drives the Gate / write path
  (`IsAnomaly` = `t1.AnyBreach`, `Severity`/`Confidence` = `FaultHeuristics`,
  `Cites` = reading + T2 ids). Stub and Foundry route identically; they differ in
  prose. DI switch `TIREFORGE_AGENTS=stub|foundry`. Impls in
  `src/TireForge.Agents/Foundry/`, driven by `tools/TireForge.AgentTool`.
- **D11** Agent SDK fallback ladder. **Rung 0 (pure C#) — ✅ CONFIRMED (session 4).**
  Nextgen **Foundry projects 2.x API**: `Azure.AI.Projects` 2.0.1 +
  `Azure.AI.Projects.Agents` 2.0.0 + `Azure.AI.Extensions.OpenAI` 2.0.0.
  `AgentAdministrationClient.CreateAgentVersion` to provision,
  `ProjectResponsesClient.CreateResponse` (+ `AgentReference`) to invoke.
  `DefaultAzureCredential` passes straight in (Azure.Core 1.53). No Python, no portal
  clicks. Rungs 1/2/3 documented in DECISIONS.md but **not needed**. Spike + full API
  notes: `tireforge/spikes/FoundryAgentSpike/FINDINGS.md`.
- **D4** `TireForge.Data` = EF Core + repo interfaces; SQLite now, Azure SQL serverless as swap target.
- **D5** schema = 5 tables incl. `Diagnoses`.
- Guiding principle: **one end-to-end run first, refine after.**

## Open vs. resolved questions

- **RESOLVED — data store (D4).** SQLite vs. another source is settled: EF Core +
  repo interfaces, SQLite for local/tests/demo, Azure SQL serverless as the
  provider-swap fallback if write contention appears, Cosmos rejected (relational
  domain). SQLite's concurrent-write risk is mitigated because the Work Order
  Adapter is the sole writer (invariant #1) — writes are serialized through one
  path. No action needed to start Stage A.
- **OPEN — APIM ↔ Foundry spike (D3/D8).** Confirm APIM `azure-openai-*` policies can
  proxy the Foundry model-deployment path. **Timebox: ~1 h.** If not clean → APIM
  moves to the roadmap; keep the TDD governance section + illustrative Cost tab +
  App Insights GenAI token spans (Stage M) as the metering path.
- **RESOLVED — architecture SVG** added to `docs/design/`.
- **RESOLVED — dashboard scope (D8).** v1.6 prototype committed as mock data
  (`docs/design/…-Dashboard_Prototype.html`). Real dashboard = a port binding the
  mock `api` to `TireForge.ApiProxy` + seeded Challenge data. **Dropped:** sim
  "drift"/"stall" scenarios (→ cut to normal/warn/crit), live gateway-429 log
  (→ static card), `WorkOrderStatus.Draft` (→ draft lives on `Diagnosis`, see D7).
- **RESOLVED — correlation / tracing (D6).** `System.Diagnostics.ActivitySource`
  (`TireForge.Pipeline`), root + child-per-step, `Diagnosis.TraceId` = W3C trace id.
  Functions hosts already wire the Azure Monitor exporter — just register the source.
  Challenge 2. Complexity low.

---

## Environment

- **Foundry (Challenge 0) deployed.** Sub `DkaySubscription`
  (`b5e77c80-223c-4cba-992e-c703545854b4`), region `swedencentral`,
  RG `foundry-hackathon-rg-3e97ae19`, account `foundry-hack-3e97ae19`,
  project `factory-project`, model deployment `gpt-5.4`.
- Config in `factory/.env` (git-ignored). If missing after a rebuild:
  `az login` then `bash tireforge/scripts/restore-env.sh` (do NOT re-run deploy.sh).
- .NET 8 SDK at `~/.dotnet` (in `~/.bashrc`); devcontainer has `dotnet` +
  `azure-functions-core-tools` features.
- Fork `github.com/dkay2017/FrontierWeekHack` (origin) · upstream `microsoft/FrontierWeekHack`.

---

## Progress by Build-Plan stage

Legend: ☐ not started · ◐ in progress · ☑ done (tests green)

| Stage | What | State |
|---|---|---|
| — | C# solution scaffold (10 projects, per-project READMEs) | ☑ commit `5a07367` |
| — | Design docs + DECISIONS.md in repo | ☑ commit `dc8ca68` |
| A | Data model — 5-table schema, seed 5 machines + bands, ~8 history incidents, data-access | ☑ `InitialCreate` migration + 14 tests green |
| B | `make_reading(machine, mode)` normal/warn/crit + `reading_id()` | ☑ `ReadingFactory` + `Ids` |
| C | ThresholdCheck (T1) pure — per-sensor status + severity + trace line | ☑ reproduces Ch1 (2 warn + 1 crit) |
| D | Anomaly Detection (A1) stubbed — `IAgentClient`, early-exit on not-anomaly | ☑ `IAnomalyDetector` + `StubAnomalyDetector` |
| E | HistoryMatch (T2) pure — fault signature + incident match | ☑ `FaultSignature` + `HistoryMatch` (exact + overlap) |
| F | Fault Diagnosis (A2) stubbed — structured `{fault,severity,confidence,text,cites}` | ☑ `IFaultDiagnoser` + `StubFaultDiagnoser` + `DiagnosisMapper` |
| G | The Gate — `gate(dx) → {route, reason}` | ☑ `Core/Gating/Gate` |
| H | Work Order draft (A3) stubbed | ☑ `IWorkOrderDrafter` + `StubWorkOrderDrafter` |
| I | Act — Adapter `write_work_order`, sole writer; auto vs review routes | ☑ `Core/Acting/WorkOrderWriter` |
| J | Compose `run_pipeline(reading)` C→D→E→F→G→H→I, one trace_id | ☑ `Core/Pipeline/Pipeline` — **first end-to-end run** |
| A1 | Persist A3 draft on every route (`Diagnosis.DraftActionText`) — D7 | ☑ `WorkOrderWriter` sets it both routes + migration |
| J.5 | Tracing — `Activity`-based correlation + host export (D6) = Challenge 2 | ☑ `Core/Observability/Telemetry`, root+child spans, hosts registered |
| K | Reviewer decisions — approve / reject / close lifecycle | ☑ `Core/Reviewing/Reviewer` — 10 tests |
| L | Report logic — `/status` `/queue` `/workorders` `/cost` + health metrics | ☑ `Core/Reporting/Reports` (14 tests) + `TireForge.ApiProxy` HTTP endpoints (5 read + 3 reviewer-write, anonymous auth per D10, 12 tests) |
| M | Real **persistent Foundry agents** (D9/D11 nextgen `Azure.AI.Projects` 2.x, D12 hybrid) — `anomaly-detection-agent` (+ `check_thresholds`) / `fault-diagnosis-agent` / `work-order-agent`, wired behind `Core.Agents`; `TIREFORGE_AGENTS=stub\|foundry` | ☑ `src/TireForge.Agents/Foundry/` + `tools/TireForge.AgentTool`; full pipeline pass verified, spans in App Insights |
| — | Ingestion Function + Storage Queue wiring | ☑ `TireForge.Ingestion` — `SensorSimulator` (timer, per-machine) + `EmitReading` (HTTP `/api/emit/{machine}/{mode}`) → `readings` queue |
| — | Orchestrator Durable wiring around `run_pipeline` | ☑ `TireForge.Orchestrator` — queue → `PipelineStarter` (dedup on reading id) → `PipelineOrchestrator` → `RunPipeline` activity = one `Core.Pipeline.RunAsync` (D2). 4 tests. |
| — | Dashboard (port of v1.6) | ☑ `src/TireForge.Dashboard/index.html` — mock `api` → live `fetch` at ApiProxy, reviewer POSTs, `gpt-5.4`, mojibake fixed, sim → illustrative; jsdom smoke test green |
| — | APIM AI Gateway + token policies | ☐ |
| — | Eval harness (4 scenarios) + Health Workbook | ☐ |
| — | `infra/` bicep — **Layer 1** `modules/foundry.bicep` (Challenge-0 stack) + **Layer 2** `modules/apps.bicep` (storage + Durable hub + `readings` queue, Flex Consumption ×3 Function Apps + MI + RBAC, Free SWA) + `modules/data.bicep` (Azure SQL serverless, D4). Agents = runtime `AgentTool provision`, not IaC. | ☑ `az bicep build` clean; not yet deployed |

---

## Next actions

**Stages A–M + dashboard + Ingestion/Orchestrator + Challenge-4 infra all done.**
124 tests green (34 Core + 46 Data + 28 Agents + 12 ApiProxy + 4 Orchestrator).
**Next: the EF SqlServer swap** (the one code task before `azd up` persists), then
Challenge 3.

**Stage M full — shipped (session 4):**
- `src/TireForge.Agents/Foundry/` — `FoundryAgentClient` (ensure + invoke w/ tool
  loop), `ThresholdsTool` (`check_thresholds` → `Core.ThresholdCheck`), `AgentPrompts`
  (anomaly/fault verbatim from `agents.py`, work-order = ours), `Foundry{Anomaly
  Detector,FaultDiagnoser,WorkOrderDrafter}` (hybrid per D12), `FoundryAgentProvisioner`.
- `DependencyInjection.AddTireForgeAgents` — `TIREFORGE_AGENTS=stub` (default) |
  `foundry`. `FaultHeuristics` (Escalate/Score) lifted from the stub into `Core/Agents`,
  shared by both.
- `tools/TireForge.AgentTool` — `provision` (version all 3) / `run` (provision + one
  full pipeline pass on CP-003 crit, tracing on).
- **Live-verified:** CP-003 crit → A1 grounded (tool called ×2), A2 LIKELY CAUSE /
  ACTIONS / URGENCY citing inc-005/006, A3 IMMEDIATE citing the reading, Gate → Review.
  App Insights: one `pipeline.run` trace, `invoke_agent anomaly-detection-agent:2` /
  `fault-diagnosis-agent:1` / `work-order-agent:1` + `chat gpt-5.4-2026-03-05` spans
  nested per step.

**Dashboard port — shipped (session 4):** `src/TireForge.Dashboard/index.html`.
The mock `api` object now `fetch`es the ApiProxy (`/status` `/queue` `/workorders`
`/cost`); Approve/Reject → `POST /api/review/*`, Close → `POST /api/workorders/{id}/close`.
Render functions unchanged — new mappers (`mapStatus`/`mapQueue`/`mapWO`/`mapCost`)
turn the real DTOs into the shapes they already expect; `MACHINES` etc. filled from
`/status` (seeded data, not the prototype roster). `statusOf`/`cell` now read the
server severity/standing instead of recomputing. `gpt-5.4` labels; mojibake fixed
(`â`→`—`, `Â·`→`·`, …); sim scenarios → `normal/warn/crit`, made explicitly
illustrative (no client-side QUEUE/WORKORDERS mutation); Cost shows call counts +
`—` for token/spend (pending the gateway). `?api=` override, `API_BASE` defaults to
same-origin `/api`, red banner on connection failure. jsdom render smoke test green.

**Step 8 — Ingestion + Orchestrator — shipped (session 4):**
- `TireForge.Ingestion` — `SensorSimulator` (timer, 5 min; one weighted
  `ReadingFactory` reading per seeded machine) + `EmitReading`
  (`POST /api/emit/{machineId}/{mode?}`) → `readings` queue. Refs `Core` + `Data`
  (machine roster from the store).
- `TireForge.Orchestrator` — `PipelineStarter` (`[QueueTrigger("readings")]`,
  `InstanceId = reading.Id` → redelivery is a no-op) → `PipelineOrchestrator`
  (deterministic) → `RunPipeline` (`[ActivityTrigger]`, injected `Pipeline`,
  returns `PipelineRunSummary`). `Program.cs` = `AddTireForgeData` +
  `AddTireForgeAgents` + `AddScoped<Pipeline>()` + migrate/seed.
- `local.settings.sample.json` for both (+ ApiProxy) point `TIREFORGE_DB` at
  `../../tireforge.db` so all local hosts share one SQLite file; `AgentTool` too.
- 4 `TireForge.Orchestrator.Tests` — DI wiring + the activity end to end
  (crit → review, normal → stop, confident warn → auto WO).

**Challenge-4 infra — done (session 4):** `infra/modules/apps.bicep` (identity-only
storage + Durable hub + `readings` queue; Flex Consumption `FC1` plan; 3 Function
Apps with system-assigned MI, App Insights, `azd-service-name` tags, CORS on
apiproxy; Free Static Web App for the dashboard; RBAC — storage blob/queue/table
data roles per identity + Cognitive Services User on Foundry for the orchestrator)
+ `infra/modules/data.bicep` (Azure SQL serverless `GP_S_Gen5_1`, auto-pause,
Entra-only auth). `main.bicep` gains `deployCompute`/`deployDatabase` toggles + the
compute outputs. `azd up` provisions both layers + deploys all 4 services.
`az bicep build` clean.

**Remaining for Challenge 4:**
1. **EF SqlServer swap** (the resume point) — `Microsoft.EntityFrameworkCore.SqlServer`,
   provider chosen from the connection string, a SqlServer migrations set, an `azd
   postprovision` hook to migrate + seed. See `infra/README.md`.
2. Live `azd up` + the portal workflow-designer steps (Challenge 4 Part 2).
3. Live multi-host `func start` smoke test — needs `azure-functions-core-tools`.

- **Agent contracts refactor:** ports (`IAnomalyDetector` / `IFaultDiagnoser` /
  `IWorkOrderDrafter`) + outputs (`AnomalyVerdict` / `FaultVerdict` /
  `WorkOrderDraft`) + `DiagnosisMapper` now live in `TireForge.Core/Agents`; stubs
  stay in `TireForge.Agents` (flat namespace — a `Diagnosis` sub-namespace
  collides with `Core.Model.Diagnosis`).
- H: `StubWorkOrderDrafter` — action templated from the diagnosis, urgency by severity.
- I: `Core/Acting/WorkOrderWriter.ActAsync` — Auto → issue WO via `IWorkOrderStore`
  (sole write path) + `Diagnosis.Status = AutoIssued`; Review → `Pending`, no WO.
- J: `Core/Pipeline/Pipeline.RunAsync(reading)` — C→D→E→F→G→H→I, one `Ids.Trace()`
  id on every step; `PipelineResult { TraceId, IsAnomaly, ThresholdSeverity,
  Diagnosis, Act, Trace[] }`. Verified: normal → stops at D (no rows); confident
  Warn (IS-005) → auto WO, fault grounded in `inc-008`; Crit (CP-003) → Review,
  pending, no WO; one trace id threads every line.

**Revised sequence (agreed session 3, see DECISIONS.md "Revised build sequence"):**
1. ✅ **A1** — `Diagnosis.DraftActionText`, set by `WorkOrderWriter` on both routes
   (`AddDiagnosisDraftActionText` migration).
2. ✅ **Tracing stage (J.5)** — `Core/Observability/Telemetry` `ActivitySource`;
   `Pipeline` emits root `pipeline.run` + a child span per step, tagged
   (`reading_id`/`machine_id`/`severity`/`confidence`/`gate_route`); `Diagnosis.TraceId`
   = W3C trace id (32-hex); all three Functions `Program.cs` register the source
   alongside the existing Azure Monitor exporter. 4 tracing tests via `ActivityListener`.
3. ✅ **Stage K** — `Core/Reviewing/Reviewer`: `ApproveAsync` / `RejectAsync` (note
   required, `Rejected` audit row) / `CloseAsync` (only from `Issued`/`Approved`).
   All writes via `IWorkOrderStore`. 10 tests.
4. ✅ **Stage L** — `Core/Reporting/Reports` (14 tests) + `TireForge.ApiProxy` HTTP endpoints:
   5 read (`/status` `/queue` `/workorders` `/health` `/cost`) + 3 reviewer-write
   (`/review/approve` `/review/reject` `/workorders/{id}/close`), anonymous auth (D10),
   `ApiJson` camelCase+enum-string wire shape, `HttpProblem` exception→status mapping.
   12 ApiProxy tests. **Pending:** live `func` host smoke test (no core-tools in this
   Codespace) — folded into the dashboard-port step.
5. ✅ **Stage M spike (D9)** — done. `spikes/FoundryAgentSpike` created + invoked
   `anomaly-detection-agent` v1 in pure C# (nextgen `Azure.AI.Projects` 2.x),
   portal-visible, trace in App Insights. Rung 0 confirmed; fallback not needed.
6. ✅ **Stage M full** — provisioner + `check_thresholds` tool + hybrid real impls
   behind the `Core.Agents` interfaces (+ `TIREFORGE_AGENTS` switch, D12).
   3 agents live = **Challenge 1 real**; nested agent spans = **Challenge 2 real**.
7. ✅ **Dashboard port** — `src/TireForge.Dashboard/index.html`: mock `api` → live `fetch` at ApiProxy, reviewer POSTs, `gpt-5.4`, mojibake fixed, sim illustrative.
8. ◐ **Ingestion + Orchestrator + Challenge-4 infra** — Durable pipeline (4 tests) + `apps.bicep`/`data.bicep` (Flex ×3 + storage + Azure SQL + SWA + RBAC) done; EF SqlServer swap + `azd up` + portal workflow remain.
9. **Challenge 3** — portal Evaluations over `eval_portal.jsonl`; `eval/TireForge.Eval` = CI-gate superset.
10. **APIM** — only if the D3 spike passes (now in front of hosted-agent calls).

### Rated change backlog (from the dashboard-prototype review)

Value = matters for a judgeable submission · Cx = build cost. Full detail in the
session-3 analysis; drops recorded in DECISIONS.md D8.

| Item | Value | Cx | Status |
|---|---|---|---|
| A1 persist draft | 8 | 2 | ✅ done |
| Activity tracing + export | 9 | 4 | ✅ done (host export untested live) |
| Stage K reviewer | 7 | 3 | ✅ done |
| Stage L — Core read models | 8 | 4 | ✅ done |
| Stage L — `TireForge.ApiProxy` endpoints | 7 | 3 | ✅ done |
| **Stage M spike — real Foundry agent, .NET SDK (D9)** | 10 | 5 | ✅ done (`spikes/FoundryAgentSpike`) |
| Stage M full — 3 hosted agents behind the interfaces | 9 | 5 | ✅ done (`Foundry/` + `AgentTool`) |
| Dashboard port (fetch, gpt-5.4 labels, mojibake, sim trim) | 7 | 3 | ✅ done |
| Ingestion + Orchestrator + Challenge-4 infra | 7 | 5 | ✅ code + bicep done (EF SqlServer swap + `azd up` remain) |
| Challenge 3 portal evaluation + `eval/TireForge.Eval` | 6 | 3 | queued |
| Cost tab real numbers | 5 | 5 | deferred → needs Stage M token spans |
| APIM gateway + policies | 6 | 8 | deferred → 1 h spike, else roadmap |
| Shorter display IDs | 4 | 2 | deferred (cosmetic) |
| Sim drift/stall, live 429 log, WO Draft state | — | — | **dropped** (D8) |

## Fresh-Codespace setup (do this first in a new Codespace)

```bash
# 1. dotnet on PATH (devcontainer installs the SDK to ~/.dotnet; also in ~/.bashrc)
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

# 2. global tools are NOT restored by a new Codespace — reinstall:
dotnet tool install --global dotnet-ef --version 8.0.11

# 3. bicep CLI (only if touching infra/):
az bicep install

# 4. Foundry config (factory/.env is git-ignored — gone on a new Codespace):
az login          # subscription DkaySubscription
bash tireforge/scripts/restore-env.sh

# 5. sanity check — expect 124 green (34 Core + 46 Data + 28 Agents + 12 ApiProxy + 4 Orchestrator):
cd tireforge && dotnet build TireForge.sln && dotnet test TireForge.sln
```

**Prefer resuming the SAME Codespace** (github.com/codespaces) — it keeps `factory/.env`,
the global tools, and the local SQLite file. Steps 2–4 are only needed on a brand-new one.

Build: `dotnet build TireForge.sln` · Test: `dotnet test TireForge.sln`
(from `tireforge/`). `dotnet-ef` migrations: see `src/TireForge.Data/README.md`.

---

## Session log

- **Session 1–2 (2026-09-02):** Challenge 0 deployed. C# scaffold created + pushed.
  Design docs transcribed into `docs/design/`. DECISIONS.md (D1–D5) agreed + committed.
  Earlier ad-hoc work (root files, docs subfolder) lost to a Codespace rebuild.
- **Session 3 (2026-09-02):** Reloaded context. Walked all three design docs.
  Created this STATUS.md. Added architecture SVG. Ported `deploy.sh` to
  `infra/main.bicep` + `infra/modules/foundry.bicep` (+ params, README) so the
  Challenge-0 stack is reproducible in another environment — compiles clean,
  not yet test-deployed. Agreed working method: challenge folders = guide only,
  build C# equivalents. Read + mapped Challenge 1.
- **Session 3 cont. — Stage A shipped.** `TireForge.Core` model + ports,
  `TireForge.Data` DbContext + 5 stores + JSON-backed seeder (5 machines, snapshot
  readings, 8 history incidents) + `InitialCreate` migration. 14 xUnit tests green.
- **Session 3 cont. — Stages B + C shipped.** `ReadingFactory` + `Ids` (Stage B),
  `ThresholdCheck`/`ThresholdReport` (Stage C, C# port of Challenge 1's
  `check_thresholds`, reproduces its 2-warn/1-crit outcome). 33 tests total.
- **Session 3 cont. — Stages D + E shipped.** `StubAnomalyDetector` (A1 stub,
  Stage D); `FaultSignature` + `HistoryMatch` (T2, Stage E, exact + overlap).
  Seed history signatures canonicalised. 42 tests green.
- **Session 3 cont. — Stages F + G shipped.** `StubFaultDiagnoser` + `FaultVerdict`
  + `DiagnosisMapper` (A2, Stage F); `Core/Gating/Gate` (Stage G). 67 tests green.
- **Session 3 cont. — Stages H + I + J shipped — first end-to-end run.**
  `StubWorkOrderDrafter` (A3); `Core/Acting/WorkOrderWriter` (Act); `Core/Pipeline`
  composes C→D→E→F→G→H→I under one trace id. Agent ports+contracts moved to
  `Core/Agents`. 80 tests green.
- **Session 3 cont. — dashboard-prototype review + scope re-eval.** v1.6 UI
  prototype committed as mock data. DECISIONS.md D6 (Activity tracing), D7 (persist
  draft), D8 (dashboard scope + APIM timebox + drops) added; rated backlog +
  revised 8-step sequence recorded.
- **Session 3 cont. — A1 + tracing stage shipped.** `Diagnosis.DraftActionText`
  (D7, `WorkOrderWriter` sets it on both routes, `AddDiagnosisDraftActionText`
  migration). `Core/Observability/Telemetry` `ActivitySource`; `Pipeline` emits a
  root + child span per step with tags; `Diagnosis.TraceId` = W3C trace id; all 3
  Functions hosts register the source next to the scaffold's Azure Monitor exporter.
  84 tests green.
- **Session 3 cont. — Stage K shipped.** `Core/Reviewing/Reviewer` — approve
  (issue drafted WO `by=reviewer`), reject (`Rejected` audit row + required note),
  close (only from `Issued`/`Approved`); all writes via the Adapter; review spans
  traced. 94 tests green.
- **Session 3 cont. — Stage L read models shipped.** `Core/Reporting/Reports` +
  `Contracts` (status/queue/workorders/health/cost response DTOs) over
  `IReportingQueries` (`TireForge.Data/Reporting/ReportingQueries`).
  `AddTireForgeData` DI helper. Cost = call counts only, token/spend null (D8).
  102 tests green (34 Core + 46 Data + 22 Agents). Pending: `TireForge.ApiProxy`
  HTTP endpoints.
- **Session 3 cont. — read all challenge READMEs → D9.** Challenges 1–4 are
  agent-keyed: they need real **persistent Foundry agents** (portal-visible, via
  `agents.create_version` / .NET Agents SDK), not just agent-shaped logic. D3's
  "direct model call" is superseded for the real path. Stage M creates the three
  named agents behind the existing `Core.Agents` interfaces; a **Stage M spike is
  brought forward** (step 5) to de-risk the .NET↔Foundry SDK before wiring.
  Ch 3 & 4 stay mostly portal work once the agents exist.
- **Session 4 (2026-09-03) — Stage L finished: `TireForge.ApiProxy` endpoints.**
  Re-read all challenge READMEs (0–4; there is no challenge-5) — mapping in the
  table above still holds. `Program.cs` wires `AddTireForgeData` + migrate/seed on
  startup + `ApiJson` (camelCase, enums as camelCase strings). `ReportsFunctions`
  = 5 GET delegates to `Reports` (`/status` `/queue` `/workorders` `/health`
  `/cost`). `ReviewFunctions` = 3 POST over the Stage-K `Reviewer`
  (`/review/approve` `/review/reject` `/workorders/{id}/close`), domain exceptions
  → problem responses via `HttpProblem` (400 / 404 / 409). **All anonymous — new
  Decision D10** (dashboard is a keyless SPA; gateway or Function key goes in front
  before any non-local deploy). New `tests/TireForge.ApiProxy.Tests` (added to
  sln): `HttpProblem` mapping, `ApiJson` wire shape, endpoint integration over a
  seeded in-memory DB. 114 tests green. **Pending:** live `func` host smoke test —
  no `azure-functions-core-tools` in this Codespace; folded into the dashboard-port
  step (7).
- **Session 4 cont. — D11 recorded, then Stage M spike shipped. Rung 0 (pure C#)
  confirmed.** `tireforge/spikes/FoundryAgentSpike` (console, own csproj, not in
  the sln): reads `factory/.env`, creates `anomaly-detection-agent` v1 via the
  nextgen **Foundry projects 2.x** .NET API (`Azure.AI.Projects` 2.0.1 +
  `Azure.AI.Projects.Agents` 2.0.0 + `Azure.AI.Extensions.OpenAI` 2.0.0 —
  `AgentAdministrationClient.CreateAgentVersion`), invokes it once via
  `ProjectResponsesClient.CreateResponse` + `AgentReference`. Output correct
  (2 warn MX-001/IS-005 + 1 crit CP-003). Confirmed portal-visible (REST
  `GET /agents` → `kind: prompt`, `status: active`, `agent_guid`). Trace in App
  Insights: dependency span `invoke_agent anomaly-detection-agent:1`, `type: AI`.
  `DefaultAzureCredential` works straight in (Azure.Core 1.53 =
  `AuthenticationTokenProvider`). No Python, no portal clicks — D11 fallback rungs
  not needed. Full API notes + Stage-M-full plan in the spike's `FINDINGS.md`.
- **Session 4 cont. — Stage M full shipped. Real Foundry agents, end-to-end.**
  `src/TireForge.Agents/Foundry/`: `FoundryAgentClient` (ensure-agent + invoke with
  the function-tool loop), `ThresholdsTool` (`check_thresholds`, body →
  `Core.ThresholdCheck`), `AgentPrompts`, `Foundry{AnomalyDetector,FaultDiagnoser,
  WorkOrderDrafter}` (**hybrid — D12**: agent writes prose, deterministic Core owns
  the gate-driving numbers), `FoundryAgentProvisioner`. `FaultHeuristics`
  (Escalate/Score) lifted from the stub into `Core/Agents`. `AddTireForgeAgents` +
  `TIREFORGE_AGENTS=stub|foundry`. New `tools/TireForge.AgentTool` (`provision` /
  `run`) — added to the sln. **Live full-pipeline pass** (CP-003 crit): A1 grounded
  + tool called ×2, A2 LIKELY CAUSE citing inc-005/006, A3 IMMEDIATE citing the
  reading, Gate → Review. App Insights: one `pipeline.run` trace with
  `invoke_agent <name>:<ver>` + `chat gpt-5.4-2026-03-05` spans nested per step =
  Challenges 1 & 2 real. 6 new Agents tests (DI switch, `check_thresholds` payload).
  120 green (34 Core + 46 Data + 28 Agents + 12 ApiProxy).
- **Session 4 cont. — dashboard port (step 7).** `src/TireForge.Dashboard/index.html`
  (from prototype v1.6). Mock `api` → real `fetch` at `TireForge.ApiProxy`; new
  `mapStatus`/`mapQueue`/`mapWO`/`mapCost` adapt the real DTOs into the shapes the
  (unchanged) render functions expect. `MACHINES`/`QUEUE`/… now filled from the
  endpoints; `statusOf`/`cell` read server severity/standing. Approve/Reject →
  `POST /api/review/*`, Close → `POST /api/workorders/{id}/close`, then re-fetch.
  Mojibake fixed (`â`→`—`, stray `Â` stripped). `gpt-5.4` labels throughout. Sim
  scenarios → `normal/warn/crit`, explicitly illustrative (no client QUEUE/WORKORDERS
  mutation). Cost tab: call counts real, token/spend `—` (pending the gateway, D8).
  `?api=` override, same-origin `/api` default, red banner on failure.
  `local.settings.sample.json` (CORS) added. jsdom render smoke test (16 checks)
  green. Live `func` smoke test still pending (no core-tools here).
- **Session 4 cont. — step 8: Ingestion + Orchestrator (Durable pipeline).**
  `TireForge.Ingestion` — `SensorSimulator` (timer 5 min, one weighted
  `ReadingFactory` reading per seeded machine) + `EmitReading`
  (`POST /api/emit/{machineId}/{mode?}`) → `readings` queue; refs `Core` + `Data`.
  `TireForge.Orchestrator` — `PipelineStarter` (`[QueueTrigger("readings")]`,
  `InstanceId = reading.Id` so redelivery is a no-op) → `PipelineOrchestrator`
  (deterministic) → `RunPipeline` (`[ActivityTrigger]`, injected `Pipeline`) =
  one `Core.Pipeline.RunAsync` (D2), returns `PipelineRunSummary`. Both `Program.cs`
  = `AddTireForgeData` + `AddTireForgeAgents` (+ `AddScoped<Pipeline>()`) + migrate/
  seed. `local.settings.sample.json` for all three hosts point `TIREFORGE_DB` at a
  shared `../../tireforge.db`; `AgentTool` uses the repo-root path too. New
  `tests/TireForge.Orchestrator.Tests` (4): DI wiring + activity end-to-end
  (crit→review, normal→stop, confident-warn→auto WO). 124 green.
- **Session 4 cont. — Challenge-4 infra.** `infra/` split into two layers.
  `modules/apps.bicep`: identity-only storage account (`allowSharedKeyAccess:false`)
  = Functions host + Durable hub + `readings` queue; Flex Consumption `FC1` plan;
  3 Function Apps (`functionAppConfig`, `dotnet-isolated` 8.0, system-assigned MI,
  App Insights, `azd-service-name` tags, CORS `*` on apiproxy); Free Static Web App
  for the dashboard; RBAC — Storage Blob Data Owner + Queue/Table Data Contributor
  per identity + **Cognitive Services User** on the Foundry account for the
  orchestrator. `modules/data.bicep`: Azure SQL serverless `GP_S_Gen5_1` (auto-pause
  1 h, 0.5–1 vCore), Entra-only auth, connection string → `TIREFORGE_DB` on all 3
  apps (D4). `main.bicep` + `main.parameters.json` gain `deployCompute` /
  `deployDatabase` / `sqlAdminObjectId` (azd `AZURE_PRINCIPAL_ID`) / `agentsMode` +
  compute outputs. `azure.yaml` dashboard gets `dist: .`. `az bicep build` clean.
  **Remaining for Challenge 4:** the EF SqlServer swap (provider + migrations +
  `azd postprovision`), then live `azd up` + the portal workflow designer.
- **Session 4 cont. — Challenge 3: `eval/TireForge.Eval` + CI.** CI-gate harness —
  replays the 10-case `evaluation_dataset.json` through `ThresholdCheck` (T1), gates
  on classification accuracy (`--min-accuracy` default 1.0). Baseline **10/10** class
  + urgency + anomaly count. `.github/workflows/tireforge-ci.yml` (new) —
  build → test → eval gate on push/PR; `ubuntu-latest` has Docker so the
  Testcontainers SQL Server tests run in CI. `docs/runbooks/challenge-3-portal-evaluation.md`
  = the manual portal Coherence/Fluency steps.
- **Session 4 cont. — SQLite → Azure SQL (SqlServer EF provider), everywhere.**
  `TireForge.Data` → `EntityFrameworkCore.SqlServer`; `UseSqlServer`; dropped the
  `DateTimeOffsetToBinaryConverter`; migrations regenerated for SqlServer. Hosts +
  `AgentTool` + samples: `TIREFORGE_DB` = a SQL Server string. New
  `tests/TireForge.TestSupport` — a shared **Testcontainers MsSql** helper (one
  container/process, fresh DB per `TestDb`), mirrors the old surface so the ~90
  data tests need no changes; `Microsoft.Data.Sqlite` removed from 3 test projects.
  Solution builds; 62 non-DB tests pass here; DB tests need Docker → CI.
- **Session 4 cont. — APIM descoped (D3) + cost metering (D13).** APIM AI Gateway
  **deliberately not built** — the `azure-openai-*` policies don't fit the
  hosted-agent Responses path and the visibility it feeds is available directly;
  governance = documented roadmap (DECISIONS D3, TDD §7). **Cost metering built
  instead:** `Core.Model.AgentCall` + `IAgentCallRecorder` (`Core.Agents`) →
  `AgentCalls` table (`AddAgentCalls` migration) + `Data/Repositories/AgentCallRecorder`.
  `FoundryAgentClient` sums tokens across the tool-loop; the 3 `Foundry*` impls
  (now **scoped**) record a row per invocation. `IReportingQueries.AgentCallTotalsAsync`;
  `Reports.CostAsync` returns real per-agent tokens + estimated spend
  ($2.50/1M in + $10/1M out for gpt-5.4) when any row has tokens, else the old
  placeholder. Dashboard Cost tab unchanged (already handles both). Builds; unit
  tests for the recorder / real-numbers branch **not yet written** (test pass).
- **Session 4 cont. — test DB: Testcontainers → in-memory SQLite (reverted).**
  The Testcontainers `MsSql` container startup **deadlocked the CI test host**
  (sync-over-async during assembly load; 3 CI runs hung 25 min). Reverted: the
  **runtime is still 100% SqlServer**; tests use **in-memory SQLite as the
  relational test double** (`tests/TireForge.TestSupport/TestDb.cs`).
  `AddTireForgeData` gained an `Action<DbContextOptionsBuilder>` overload → the
  Data project stays SqlServer-only, the test project injects `UseSqlite`.
  `ConfigureConventions` applies `DateTimeOffsetToBinaryConverter` **only on
  SQLite**; `InitializeTireForgeDataAsync` → `EnsureCreated` on SQLite / `Migrate`
  on SqlServer. **All 124 tests green in the Codespace, ~5 s, no Docker.** CI
  workflow: dropped the Ryuk env, `timeout-minutes: 15`.
- **Session 4 cont. — cost-metering tests + Challenges 3 & 4 portal.**
  `tests/TireForge.Data.Tests/AgentCostTests.cs` (3 tests) → **127 green**.
  **Challenge 3:** portal Evaluation on `anomaly-detection-agent`
  (Coherence + Fluency) → **100/100** (done by DK in the portal).
  **Challenge 4:** portal workflow `factory-health-workflow-portal` built +
  preview-tested — 2 agents (`anomaly-detection-agent → fault-diagnosis-agent`),
  the 3rd agent + Gate stay in the Durable orchestrator (**D14**).
- **Session 4 cont. — live deploy, DONE + verified.** `azd env` `tf1` reuses the
  existing Foundry stack (`DEPLOY_FOUNDRY=false`, `EXISTING_*` connection strings).
  **Flex Consumption abandoned** — the 3 Flex apps 404'd on every route with zero
  telemetry (dotnet-isolated + identity-based storage worker wouldn't stay up);
  `apps.bicep` rewritten to **classic Consumption (Y1) Linux** (storage connection
  string, `WEBSITE_RUN_FROM_PACKAGE`). The `tf1` Function App names were
  soft-deleted by the rebuild and App Service has no purge API → new
  `functionAppSuffix` param (`FUNCTION_APP_SUFFIX=-v2`), live apps are
  `tireforge-*-tf1-v2`. `tools/TireForge.DbDeploy` run against Azure SQL:
  migrations applied, seeded (5 machines / 8 history), the 3 `-v2` managed
  identities granted `db_datareader`+`db_datawriter`. All 4 services deployed.
  **End-to-end verified** (`emit CP-003 crit` → queue → Durable → 3 Foundry
  agents → Gate → review row; `/api/cost` real tokens = D13 live; `/api/status`
  from Azure SQL). Sensor timer disabled at stop (`AzureWebJobs.SensorSimulator.Disabled=true`)
  so it doesn't burn Foundry tokens overnight — re-enable before demoing.
  **Open:** orchestrator App Insights telemetry is empty on Y1 (OTel exporter
  quirk — functionally fine, everything downstream proves the run). Codespace SQL
  firewall rule `codespace-deploy` = egress `20.61.127.49` (changes on restart).
- **Session 4 cont. — D15: dashboard host → SWA Standard.** Decided (not yet
  applied). SWA **Free** has no *linked backend* → the dashboard reaches the API
  cross-origin via `?api=` + CORS `*`. Storage `$web` static hosting has the same
  gap (rejected). SWA **Standard** (~$9/mo) + a `linkedBackends` resource → API at
  same-origin `/api`, no CORS, no querystring. Bicep change + re-provision is the
  first task next session. See DECISIONS **D15**, PENDING-DOC-UPDATES §4.

- **Session 5 cont. — D17: T0 TrendCheck (predictive early warning), full
  vertical slice, deployed nowhere yet.** Triggered by a strict self-assessment
  against the official judging rubric (`JUDGING-SELF-ASSESSMENT.md`): Innovation
  scored 16/30, capped by "the pipeline only reacts after a breach, despite the
  product being named *predictive* maintenance." Built the fix:
  `Core.Trends.TrendCheck` (T0) — deterministic linear-trend fit per sensor over
  the recent-reading window `Pipeline` already fetches for A1, flags an in-spec
  sensor projected to breach within 24h (rate, ETA, R² confidence, `MinPoints=3`,
  `MinConfidence=0.6`); skips any sensor T1 already reports breaching so the two
  never double-signal. 8 new Core tests (steady rise, flat, improving, falling
  toward the low bound, noisy fit rejected, beyond-horizon rejected, insufficient
  history, already-breaching sensor left to T1). `EarlyWarning` model (7th table,
  `AddEarlyWarnings` migration) + `EarlyWarningStore`; `Pipeline.RunAsync` raises
  warnings alongside A1, advisory only — never gates Diagnosis/WorkOrder, never
  stops the pipeline. `Reports.WarningsAsync` + `GET /api/warnings`. Dashboard:
  new **Early Warnings** tab next to Pending Review (machine, sensor, trend,
  confidence bar, projected-breach ETA; row hover shows the full narrative).
  **136/136 tests green.** Deliberately **not** built alongside it: a 4th Foundry
  agent to narrate the warning — the feature is fully functional on the
  deterministic narrative alone; the agent is a portal-visibility fast-follow
  (D17). See DECISIONS **D17** for the full reasoning.
- **Session 5 cont. — rebrand: TireForge Industries → Meridian Tire
  Manufacturing (display layer only).** "TireForge Industries" turned out to be
  the challenge scenario's own fictional company name, verbatim in our agent
  prompts (ported from `agents.py`) and seed data — a free "used the given
  scenario as-is" tell for a judge. Renamed the 3 agent system prompts, seed
  data's `factory` field, and the dashboard title/brand ("Meridian Anomaly &
  Predictive IQ"). Explicitly **not** renamed: any folder, C# namespace, or Azure
  resource — a judge never sees those, and a full rename risks re-provisioning
  everything for zero score benefit (discussed and agreed explicitly — see
  DECISIONS D17). **Consequence, not yet actioned:** the 3 live Foundry agents
  still serve the old prompt version — a `provision` run is needed to push the
  rename live, and the Challenge 3 portal eval (100/100) was scored against the
  prior version.
- **Session 5 end — stopping for the day.** Everything above is committed and
  pushed; nothing has been re-provisioned or redeployed to Azure yet. Session 6
  punch list is at the top of this file.
