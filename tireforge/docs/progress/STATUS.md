# TireForge — build status / session context

**Purpose:** rehydrate context fast after a Codespace or Claude-session restart.
Keep this file current at every checkpoint and **commit + push it** — a Codespace
rebuild loses anything uncommitted (see the `codespace-data-loss` memory).

_Last updated: 2026-09-02 (session 3) — Stages A–J done + A1/tracing in progress_

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

| Challenge | Acceptance bar | tireforge work |
|---|---|---|
| 0 ✅ | Foundry infra | done (+ `infra/main.bicep`) |
| 1 | Anomaly agent flags the 2 warning + 1 crit machine; Fault Diagnosis gives sane actions | Stages A–G, J stubbed → M1–M2 real (`gpt-5.4`) |
| 2 | App Insights GenAI tracing, one trace per reading | `Core/Observability` `ActivitySource` + per-step spans (J.5); Azure Monitor exporter already in the Functions hosts |
| 3 | Evaluation harness | `eval/TireForge.Eval` — 4 scenarios, LLM-judge |
| 4 | Multi-agent workflow deployed | `TireForge.Orchestrator` Durable + `TireForge.Ingestion` + Work Order agent (H–I) + Functions infra |
| superset | APIM gateway · Dashboard · Reviewer gate · Work Order Adapter | after Ch4, in TDD §8 priority order |

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
- **D3** APIM built LAST; until then agents call the model deployment directly.
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
| K | Reviewer decisions — approve / reject / close lifecycle | ☐ |
| L | Report logic — `/status` `/queue` `/workorders` `/cost` + health metrics | ☐ |
| M | Swap stubs for real Foundry agents, one at a time | ☐ |
| — | Ingestion Function + Storage Queue wiring | ☐ |
| — | Orchestrator Durable wiring around `run_pipeline` | ☐ |
| — | Dashboard (port of v1.6) | ☐ |
| — | APIM AI Gateway + token policies | ☐ |
| — | Eval harness (4 scenarios) + Health Workbook | ☐ |
| — | `infra/main.bicep` — Bicep port of `deploy.sh` (Challenge-0 Foundry stack) | ☑ compiles; not yet deployed |

---

## Next actions

**Stages A–J done — the pure logic pipeline runs end to end.** 80 tests green
(34 Core + 24 Data + 22 Agents).

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
3. **Stage K** — reviewer approve / reject / close. ← **next**
4. **Stage L** — read models `/status /queue /workorders /cost` + health metrics + `TireForge.ApiProxy`.
5. **Dashboard port** — `TireForge.Dashboard`: real `fetch`, `gpt-5.4` labels, mojibake
   fix, sim cut to normal/warn/crit.
6. **Ingestion + Orchestrator wiring** — timer → queue → Durable → `Pipeline.RunAsync` = Challenge 4 shape.
7. **Stage M** — real `gpt-5.4` agents, one at a time = Challenge 1 passed for real.
8. **APIM** — only if the D3/D8 spike passes and time remains.

### Rated change backlog (from the dashboard-prototype review)

Value = matters for a judgeable submission · Cx = build cost. Full detail in the
session-3 analysis; drops recorded in DECISIONS.md D8.

| Item | Value | Cx | Status |
|---|---|---|---|
| A1 persist draft | 8 | 2 | ✅ done |
| Activity tracing + export | 9 | 4 | ✅ done (host export untested live) |
| Stage K reviewer | 7 | 3 | queued |
| Stage L read models + ApiProxy | 8 | 4 | queued |
| Dashboard port (fetch, gpt-5.4 labels, mojibake, sim trim) | 7 | 3 | queued |
| Ingestion + Orchestrator wiring | 7 | 4 | queued |
| Stage M real agents | 9 | 5 | queued |
| Cost tab real numbers | 5 | 5 | deferred → needs Stage M token spans |
| APIM gateway + policies | 6 | 8 | deferred → 1 h spike, else roadmap |
| Shorter display IDs | 4 | 2 | deferred (cosmetic) |
| Sim drift/stall, live 429 log, WO Draft state | — | — | **dropped** (D8) |

## `dotnet` / EF note (Codespace)

`dotnet` SDK is at `~/.dotnet`; `dotnet-ef` at `~/.dotnet/tools`. Both need:
```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```
Build: `dotnet build TireForge.sln` · Test: `dotnet test TireForge.sln`

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
  84 tests green (34 Core + 28 Data + 22 Agents). Next: Stage K.
