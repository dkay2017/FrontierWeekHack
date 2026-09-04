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

**SWA Free → Standard (D15).** Implemented session 5 — see §6.

**Doc/design updates still owed:**
- **STATUS.md** — the "Flex Consumption ×3" phrasing everywhere (stage table,
  session log, infra bullet, D8/rated-backlog); resume point; the live URLs;
  `-v2` app names.
- **DECISIONS D8** — "Flex Consumption `FC1`" → "classic Consumption `Y1`".
- **`infra/README.md`** — Flex → Y1; the `functionAppSuffix` escape hatch; SWA
  Standard + linked backend; the storage/keyvault module split (§6).
- **Architecture SVG / TDD** — any "Flex Consumption" label.
- **`docs/design/README.md`** — infra module description.

---

## 6. Security: Key Vault + identity-based storage + SWA Standard (D15/D16)

**Made:** session 5 (2026-09-04). See DECISIONS **D16** (+ **D15**).

**Infra changed:**
- `infra/modules/storage.bicep` (**NEW**) — the storage account split out of
  `apps.bicep` so `keyvault.bicep` can read its key before the Function Apps exist.
- `infra/modules/keyvault.bicep` (**NEW**) — RBAC-mode Key Vault; secrets
  `storage-connection-string` + `appinsights-connection-string`.
- `apps.bicep` — `AzureWebJobsStorage` identity-based (`__accountName` + service
  URIs); per-identity **Storage Blob Data Owner** + **Queue/Table Data
  Contributor** + **Key Vault Secrets User**; `APPLICATIONINSIGHTS_CONNECTION_STRING`
  and `WEBSITE_CONTENTAZUREFILECONNECTIONSTRING` are `@Microsoft.KeyVault(SecretUri=…)`
  references; `keyVaultReferenceIdentity: SystemAssigned`. SWA `sku` Free →
  Standard + `linkedBackends` → apiproxy (D15).
- `main.bicep` / `main.parameters.json` — `storage` + `keyvault` modules wired;
  azd bindings `STATIC_WEB_APP_SKU` / `STORAGE_IDENTITY_BASED` /
  `CONTENT_SHARE_KEY_IN_VAULT`. New output `KEY_VAULT_NAME`.

**Doc/design updates still owed:**
- **TDD — add a "Security" section.** Outline (write in the reconciliation pass):
  1. **Identity model** — system-assigned MI per Function App; the table from
     DECISIONS D16 (SQL / Foundry / storage / App Insights / content share → auth
     mechanism → secret? ). "MI first, Key Vault for the residual, zero static
     credentials in code or `local.settings`."
  2. **RBAC least-privilege** — exact role per identity and why (Blob Data Owner =
     Durable leases; Queue Data Contributor = `readings` + control queues; Table =
     Durable history; Cognitive Services User = agent invoke; KV Secrets User =
     the two references; SQL `db_datareader`/`db_datawriter` via `DbDeploy`, admin
     is break-glass Entra only).
  3. **Secret management** — Key Vault (RBAC mode, soft-delete), the two secrets,
     `@Microsoft.KeyVault` references, unversioned URIs = auto-rotation. The one
     platform-forced secret (content share) and why it can't be MI on Y1.
  4. **Network** — HTTPS-only, TLS 1.2 floor, FTPS disabled, SQL `Encrypt=True` +
     Entra-only + `azureADOnlyAuthentication`, storage `allowBlobPublicAccess:false`.
     Known gaps: SQL firewall `AllowAllAzureIps`, no Private Endpoints, apiproxy
     anonymous (D10) — all deliberate scope calls, Private Endpoint = prod step.
  5. **Data-plane auth to the API** — apiproxy anonymous (D10); with SWA Standard
     the dashboard reaches it same-origin through the linked backend; direct
     `*.azurewebsites.net` access stays open + CORS `*` (relax later).
  6. **App Insights / audit trail** — every pipeline run traced (W3C), agent spans,
     `AgentCalls` cost ledger; no PII in telemetry (machine + reading ids only).
  7. **AI governance** — APIM AI Gateway considered, descoped (D3); token metering
     is done in-process (D13).
  8. **Supply chain** — pinned SDK versions, `dotnet` deterministic build, CI gate.
- **DECISIONS D8** — Y1 storage note ("Y1 uses the key") is now wrong — identity-based.
- **`infra/README.md`** — the module list (storage / keyvault / apps / data /
  foundry), the toggles, the staged `CONTENT_SHARE_KEY_IN_VAULT` rollout.
- **Architecture SVG** — add the Key Vault; mark MI arrows.

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
