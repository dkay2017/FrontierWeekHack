# TireForge.ApiProxy

HTTP API for the dashboard. Azure Functions (isolated worker, .NET 8, ASP.NET Core
integration). The C# equivalent of the FastAPI sketch in
`factory/challenge-4-deploy/README.md` ("Option 3/4").

## Endpoints

Route prefix is the Functions default (`/api`). All **anonymous** for now
(Decision **D10** — a gateway or a Function key goes in front before any
non-local deploy).

### Read models — thin delegates to `TireForge.Core.Reporting.Reports` (Stage L)

| Method | Route | Returns |
|---|---|---|
| GET | `/api/status` | `StatusResponse` — 5 machines, bands, latest reading, standing severity, anomalies-24h |
| GET | `/api/queue` | `QueueResponse` — pending `Diagnoses` + full A1/T2/A2 trace + draft |
| GET | `/api/workorders` | `WorkOrdersResponse` — work-order log + lifecycle + issuer |
| GET | `/api/health` | `HealthResponse` — in-spec count, open/closed, resolution rate, anomalies by machine |
| GET | `/api/cost` | `CostResponse` — per-agent call counts (token/spend null until Stage M, D8) |

### Reviewer write path — wraps `TireForge.Core.Reviewing.Reviewer` (Stage K)

| Method | Route | Body |
|---|---|---|
| POST | `/api/review/approve` | `{ "diagnosisId": "...", "reviewer": "..." }` |
| POST | `/api/review/reject` | `{ "diagnosisId": "...", "reviewer": "...", "note": "..." }` |
| POST | `/api/workorders/{id}/close` | — |

Every write still flows through the Work Order Adapter (invariant 1.1). Domain
errors map to problem responses via `HttpProblem`: bad input → 400, unknown
entity → 404, bad state transition → 409.

## JSON shape

`ApiJson` — camelCase properties, enums as camelCase strings (`Severity.Crit` →
`"crit"`), so the dashboard's mock `api` object maps straight onto the responses.

## Configuration

| Var | Default | Purpose |
|---|---|---|
| `TIREFORGE_DB` | `Data Source=tireforge.db` | SQLite connection string (matches `TireForgeDbContextFactory`) |
| `TIREFORGE_SKIP_DB_INIT` | *(unset)* | set `true` to skip migrate + seed on startup |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | *(unset)* | enables the Azure Monitor trace exporter |

## Run locally

```bash
func start                       # requires azure-functions-core-tools
func start --cors "*"            # if serving TireForge.Dashboard from a separate origin
```

`local.settings.json` is git-ignored; see `local.settings.sample.json` for the
shape (SQLite path, App Insights connection string, `Host.CORS`).

Tests: `dotnet test tests/TireForge.ApiProxy.Tests` — `HttpProblem` mapping,
`ApiJson` wire shape, and endpoint integration over a seeded in-memory DB.

References `TireForge.Core`, `TireForge.Data`.
