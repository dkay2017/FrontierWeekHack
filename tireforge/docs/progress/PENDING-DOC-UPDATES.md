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
- **Tests** — the **runtime** has zero SQLite. Tests use **in-memory SQLite as the
  relational test double** (`tests/TireForge.TestSupport/TestDb.cs`): fast, offline,
  hermetic. `AddTireForgeData` gained an `Action<DbContextOptionsBuilder>` overload
  so the Data project stays SqlServer-only and the test project injects `UseSqlite`.
  `TireForgeDbContext.ConfigureConventions` applies the
  `DateTimeOffsetToBinaryConverter` **only on SQLite** (SqlServer keeps native
  `datetimeoffset`). `InitializeTireForgeDataAsync` → `EnsureCreated` on SQLite,
  `Migrate` on SqlServer.
  _(Testcontainers.MsSql was tried first and reverted — the container startup
  deadlocked the CI test host. See the session log.)_

**Doc/design updates still owed:**
- **DECISIONS D4** — rewrite: SQLite is gone from the **runtime** (Azure SQL is the
  store); tests use in-memory SQLite as the relational test double; the
  `AddTireForgeData(Action<...>)` overload is how.
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

**Verification:** ✅ all **124 tests pass in the Codespace** (in-memory SQLite,
~5 s, no Docker). CI green.

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

**Verification:** ✅ `tests/TireForge.Data.Tests/AgentCostTests.cs` added (3 tests —
recorder writes a row per invocation, `CostAsync` pending with no metered calls,
`CostAsync` aggregates tokens + prices spend per agent). **127 tests green.**
Still owed: one live `AgentTool run` against Azure to confirm real token rows land
end-to-end via the deployed orchestrator.

---

## 4. Compute host: Flex Consumption → classic Consumption (Y1) + SWA Standard (D15)

**Made:** end of session 4 (2026-09-03), during the live `azd up`.

**Flex Consumption → Y1.** `infra/modules/apps.bicep` was rewritten from Flex
Consumption (`FC1`, identity-based storage, `functionAppConfig`) to **classic
Consumption (`Y1`/Dynamic) Linux** (`DOTNET-ISOLATED|8.0`, storage **connection
string** — `allowSharedKeyAccess: true`, `WEBSITE_RUN_FROM_PACKAGE`, `WEBSITE_CONTENTSHARE`).
Reason: the Flex + dotnet-isolated + identity-based-storage combination would not
keep the worker running (404 on all routes, zero App Insights telemetry). Y1 is
the mature azd path. Storage RBAC role assignments for the app identities were
dropped (Y1 uses the key); the **Cognitive Services User** grant on Foundry for
the orchestrator stays.

**`functionAppSuffix` param.** New `main.bicep` / `apps.bicep` param (azd binding
`FUNCTION_APP_SUFFIX`), set to `-v2` for env `tf1` — the original
`tireforge-*-tf1` Function App names were **soft-deleted** (Flex→Y1 rebuild) and
App Service has no purge API, so the live apps are `tireforge-{ingestion,
orchestrator,apiproxy}-tf1-v2`. A clean future environment leaves the suffix empty.

**SWA Free → Standard (D15).** See DECISIONS **D15**. `sku` Free → Standard,
add a `Microsoft.Web/staticSites/linkedBackends` → `tireforge-apiproxy`, dashboard
`API_BASE` defaults to same-origin `/api`, drop the `?api=` + CORS `*` workaround.
**Bicep change + re-provision still pending** (stopped right after the Y1 compute
stack went live).

**Doc/design updates still owed:**
- **STATUS.md** — the "Flex Consumption ×3" phrasing everywhere (stage table,
  session log, infra bullet, D8/rated-backlog); resume point; the live URLs;
  `-v2` app names.
- **DECISIONS D8** — "Flex Consumption `FC1`" → "classic Consumption `Y1`".
- **`infra/README.md`** — Flex → Y1; the `functionAppSuffix` escape hatch; SWA
  Standard + linked backend.
- **Architecture SVG / TDD** — any "Flex Consumption" label.
- **`docs/design/README.md`** — infra module description.

---

## 5. Challenges 3 & 4 — portal work completed

**Done (session 4):**
- **Challenge 3** — portal Evaluation run on `anomaly-detection-agent` (Coherence +
  Fluency over the eval set): **100 / 100**. Runbook
  `docs/runbooks/challenge-3-portal-evaluation.md` reflects the actual steps taken.
- **Challenge 4** — portal workflow `factory-health-workflow-portal` built +
  preview-tested (2 agents, D14).

**Doc/design updates still owed:**
- **STATUS.md** — Challenge 3 row → ✅ (portal 100/100); Challenge 4 row → ✅
  (portal workflow built); the "⬜ unblocked / 🟡 pending" states; session log.
