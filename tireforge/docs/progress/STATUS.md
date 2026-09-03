# TireForge — build status / session context

**Purpose:** rehydrate context fast after a Codespace or Claude-session restart.
Keep this file current at every checkpoint and **commit + push it** — a Codespace
rebuild loses anything uncommitted (see the `codespace-data-loss` memory).

_Last updated: 2026-09-03 (session 4). Resume point: **step 7 — dashboard port**
(`TireForge.Dashboard`: real `fetch` against `ApiProxy`, `gpt-5.4` labels, mojibake
fix, sim → normal/warn/crit; also the deferred `func` host smoke test).
**Stages A–L + Stage M (spike + full) all done.** Real Foundry agents live and
verified end-to-end — see the session log + DECISIONS.md D12. 120 tests green
(34 Core + 46 Data + 28 Agents + 12 ApiProxy)._

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
| 4 | agents visible as persistent assets + portal workflow designer | 3 agents live ✅; `TireForge.Ingestion`/`Orchestrator` wiring + portal workflow designer outstanding | 🟡 agents done, wiring pending |
| superset | APIM gateway · Dashboard · Reviewer gate · Work Order Adapter | Reviewer ✅, read models ✅; ApiProxy + dashboard port pending; APIM last | 🟡 partial |

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
| — | Ingestion Function + Storage Queue wiring | ☐ |
| — | Orchestrator Durable wiring around `run_pipeline` | ☐ |
| — | Dashboard (port of v1.6) | ☐ |
| — | APIM AI Gateway + token policies | ☐ |
| — | Eval harness (4 scenarios) + Health Workbook | ☐ |
| — | `infra/main.bicep` — Bicep port of `deploy.sh` (Challenge-0 Foundry stack) | ☑ compiles; not yet deployed |

---

## Next actions

**Stages A–L + Stage M (spike + full) all done.** 120 tests green (34 Core +
46 Data + 28 Agents + 12 ApiProxy). **Next: step 7 — dashboard port.**

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

**Dashboard port — the plan (step 7):**
1. `TireForge.Dashboard` = the v1.6 prototype HTML, `api` object → real `fetch` at
   `TireForge.ApiProxy` (`/api/status` `/queue` `/workorders` `/health` `/cost`).
2. Reconcile machine roster / numbers to the seeded Challenge data; `gpt-5.4` labels;
   fix the mojibake; sim → `normal / warn / crit` only (D8 drops).
3. Serve it (static files off the ApiProxy Functions host, or a tiny separate host).
4. Fold in the deferred **`func` host smoke test** — needs `azure-functions-core-tools`
   (not in this Codespace; `npm i -g azure-functions-core-tools@4` or the devcontainer feature).

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
7. **Dashboard port** — real `fetch`, `gpt-5.4` labels, mojibake fix, sim → normal/warn/crit. ← **next**
8. **Ingestion + Orchestrator + `azd up`** = **Challenge 4** (Functions path) → then portal workflow steps.
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
| Dashboard port (fetch, gpt-5.4 labels, mojibake, sim trim) | 7 | 3 | queued |
| Ingestion + Orchestrator wiring + `azd up` | 7 | 5 | queued |
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

# 5. sanity check — expect 120 green (34 Core + 46 Data + 28 Agents + 12 ApiProxy):
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
