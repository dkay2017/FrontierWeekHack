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
