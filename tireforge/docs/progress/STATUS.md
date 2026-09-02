# TireForge — build status / session context

**Purpose:** rehydrate context fast after a Codespace or Claude-session restart.
Keep this file current at every checkpoint and **commit + push it** — a Codespace
rebuild loses anything uncommitted (see the `codespace-data-loss` memory).

_Last updated: 2026-09-02 (session 3)_

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
| 2 | App Insights GenAI tracing, one trace per reading | trace_id in Stage J + OTel exporter in `TireForge.Agents` |
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
- **OPEN — APIM ↔ Foundry spike (D3).** Confirm APIM `azure-openai-*` policies can
  proxy the Foundry model-deployment path before building the gateway. Deferred to
  last (governance layer). Fallback: per-agent APIM products + feed the Cost tab
  from App Insights GenAI trace token counts.
- **RESOLVED — architecture SVG** added to `docs/design/`.

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
| B | `make_reading(machine, mode)` normal/warn/crit + `reading_id()` | ☐ |
| C | ThresholdCheck (T1) pure — per-sensor status + severity + trace line | ☐ |
| D | Anomaly Detection (A1) stubbed — `IAgentClient`, early-exit on not-anomaly | ☐ |
| E | HistoryMatch (T2) pure — fault signature + incident match | ☐ |
| F | Fault Diagnosis (A2) stubbed — structured `{fault,severity,confidence,text,cites}` | ☐ |
| G | The Gate — `gate(dx) → {route, reason}` | ☐ |
| H | Work Order draft (A3) stubbed | ☐ |
| I | Act — Adapter `write_work_order`, sole writer; auto vs review routes | ☐ |
| J | Compose `run_pipeline(reading)` C→D→E→F→G→H→I, one trace_id | ☐ |
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

**Stage A is done** — entities + enums in `TireForge.Core/Model`, ports in
`TireForge.Core/Abstractions`, `TireForgeDbContext` + stores + seeder +
`InitialCreate` migration in `TireForge.Data`, 14 tests green.

1. **Stage B** — `ReadingFactory.Make(machine, mode)` (normal/warn/crit) +
   `ReadingId.New()` → `rdg-<ticks>-<rand>`, in `TireForge.Core`. Tests: normal
   in-band, crit out of band, ids unique + sortable.
2. **Stage C** — `ThresholdCheck` (T1): per-sensor `{value, band, status:
   ok/low/high, deviation%}` + worst-deviation severity + a trace line citing the
   `rdg-id` and offending sensor. This is the C# port of Challenge 1's
   `check_thresholds`. Table-driven tests against the 5 seeded machines.

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
  readings, 8 history incidents) + `InitialCreate` migration. 14 xUnit tests green,
  full solution builds. Next: Stage B + C.
