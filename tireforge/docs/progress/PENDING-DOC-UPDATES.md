# Pending documentation / design updates

Changes made to the code that the **design docs + STATUS have NOT yet been
reconciled to**. Do one consolidated pass at the end.

---

## 1. Data store: SQLite → Azure SQL (SqlServer EF provider) — everywhere

**Made:** session 4 (2026-09-03). **Reason:** Flex Consumption has no persistent
local disk; three Function Apps must share one DB; concurrent Orchestrator
instances break SQLite's file lock. Azure SQL serverless (auto-pause) is the
target (was already D4's fallback — now promoted to the only store).

**Code changed:**
- `TireForge.Data` — `Microsoft.EntityFrameworkCore.Sqlite` → `.SqlServer`;
  `AddTireForgeData` / `TireForgeDbContextFactory` → `UseSqlServer`.
- `TireForgeDbContext` — removed the `DateTimeOffsetToBinaryConverter`
  (SQL Server stores `datetimeoffset` natively and orders it correctly).
- `Migrations/` — regenerated for the SqlServer provider (the SQLite
  `InitialCreate` + `AddDiagnosisDraftActionText` are replaced).
- `Program.cs` (Ingestion / Orchestrator / ApiProxy) + `AgentTool` +
  `local.settings.sample.json` — default `TIREFORGE_DB` is now a SqlServer
  connection string (localhost dev / Azure SQL prod), not `Data Source=…`.
- Tests (`TireForge.Data.Tests`, `TireForge.ApiProxy.Tests`,
  `TireForge.Orchestrator.Tests`) — `Microsoft.Data.Sqlite` /
  `EntityFrameworkCore.Sqlite` removed; the in-memory SQLite test DB replaced by
  **Testcontainers.MsSql** (a throwaway SQL Server container per run).

**Doc/design updates still owed:**
- **DECISIONS D4** — rewrite: SQLite is gone, not "local/tests only"; Azure SQL
  is the store; note the Testcontainers test strategy and the
  connection-string-driven provider is no longer needed (single provider).
- **DECISIONS D5** — schema note still says "SQLite `History` table" etc.
- **STATUS.md** — every "SQLite" mention (working method, stage table, D4 summary,
  fresh-Codespace setup, session log, sanity-check line "expect N green").
- **STATUS.md — resume point / "the EF SqlServer swap"** — that task is now DONE;
  what remains is: regenerate/verify migrations against real Azure SQL, the
  `azd postprovision` migrate+seed hook, live `azd up`.
- **TDD** (`docs/design/…-TDD.md`) — any data-layer / persistence section.
- **`docs/design/README.md`** (design→project map) — data layer description.
- **Architecture SVG** — if it labels the store "SQLite".
- **`infra/README.md`** — the "one code task before azd up persists" section is
  now (mostly) done; reframe as "migrations + postprovision hook".
- **Per-project READMEs** — `TireForge.Data`, `TireForge.Ingestion`,
  `TireForge.Orchestrator`, `TireForge.ApiProxy` (the `../../tireforge.db`
  sharing note), `tools/TireForge.AgentTool`, `spikes/…/FINDINGS.md` if it
  mentions the db.

**Verification owed:** `dotnet test` was **not run in the Codespace** for this
change (no Docker for Testcontainers). Must be run on a Docker-capable machine
(local repo / CI) — this is the agreed "develop in Codespace, test + fix
locally" split.

---

## 2. Challenge 3 — `eval/TireForge.Eval` + CI + portal runbook

**Made:** session 4 (2026-09-03).

**Code / infra added:**
- `eval/TireForge.Eval` — CI-gate harness. Replays the 10-case
  `evaluation_dataset.json` through `ThresholdCheck` (T1), gates on classification
  accuracy (`--min-accuracy`, default 1.0). Current baseline **10/10** class +
  urgency + anomaly count. `--json` report. Runs offline in ~1 s.
- `.github/workflows/tireforge-ci.yml` — new. `build → test → eval gate` on push /
  PR touching `tireforge/**`. Runs on `ubuntu-latest` (has Docker → the
  Testcontainers SQL Server tests run here — this is where the SQLite→SqlServer
  swap gets verified).
- `docs/runbooks/challenge-3-portal-evaluation.md` — the manual portal
  Coherence/Fluency steps.

**Doc/design updates still owed:**
- **STATUS.md** — Challenge 3 row (⬜ → 🟡: `TireForge.Eval` + CI done, portal
  Coherence/Fluency run still manual/pending); revised-sequence step 9; add
  `eval/TireForge.Eval` + `.github/workflows/tireforge-ci.yml` to the project
  inventory; session log; "CI" is now a thing (was noted as absent).
- **DECISIONS** — no new decision needed, but the D9 note "`eval/TireForge.Eval` =
  CI-gate superset" can move from "planned" to "done".
- **`docs/design/README.md`** — add the `eval/` + CI entries.
- **`eval/TireForge.Eval/README.md`** — written now, keep in the reconciliation
  sweep for consistency of terminology.

---

## 3. APIM descoped (D3) + cost metering added (D13)

**DECISIONS D3 + D13 + TDD §7 — already updated** (commit `031c248`). The rest:

**Code (this session):**
- `Core.Model.AgentCall` + `Core.Agents.IAgentCallRecorder` / `AgentCallUsage` /
  `NullAgentCallRecorder`.
- `TireForge.Data` — `AgentCalls` DbSet + config + `AddAgentCalls` migration +
  `Repositories/AgentCallRecorder`. `IReportingQueries.AgentCallTotalsAsync` +
  `AgentCallTotals` record.
- `FoundryAgentClient.InvokeAsync` — sums tokens across the tool-loop.
- `Foundry{AnomalyDetector,FaultDiagnoser,WorkOrderDrafter}` — now take
  `IAgentCallRecorder`, record a row per invocation; **registered `AddScoped`**
  (was `AddSingleton`) so they share the pipeline's scope.
- `AddTireForgeAgents` — `TryAddScoped<IAgentCallRecorder, NullAgentCallRecorder>`;
  `AddTireForgeData` — `RemoveAll` + `AddScoped<AgentCallRecorder>` (real one wins).
- `Reports.CostAsync` — if any `AgentCalls` row has tokens → real per-agent tokens
  + estimated spend (`$2.50/1M in + $10/1M out` for gpt-5.4, `TokenMetricsAvailable=true`);
  else the old call-count placeholder. Dashboard needs no change.

**Doc/design updates still owed:**
- **STATUS.md** — Challenge-4/superset "APIM" row → descoped; add `AgentCall` to the
  schema (D5 says 5 tables — now 6); Cost tab note ("call counts, token/spend
  pending" → "real when foundry"); session log; resume point.
- **DECISIONS D5** — "5 tables" → 6 (`AgentCalls`).
- **infra** — `data.bicep` / `apps.bicep` are unaffected (APIM was never in bicep);
  the `infra/README.md` "Still not in Bicep — APIM" line can note "descoped, not
  roadmapped for the submission".
- **TDD §4/§8** — the "SQLite" line in §8 (still there) + any APIM-as-planned prose
  in §2/§3/§5.
- **Architecture SVG** — if it draws an APIM box in the main flow, mark it
  roadmap / dashed.

**Verification owed:** new metering path is compile-verified + 62 non-DB tests
green in the Codespace; the `AgentCalls` migration + recorder + `CostAsync` need a
Docker run (CI) and, ideally, one live `AgentTool run` to confirm real token rows
land. **No new unit tests written yet** for `AgentCallRecorder` / the real-numbers
`CostAsync` branch — add in the test pass.
