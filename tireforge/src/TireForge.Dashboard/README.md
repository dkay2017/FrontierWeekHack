# TireForge.Dashboard

The **Experience** layer — the reviewer console. A single static `index.html`
(port of the v1.6 prototype), no build step, no `.csproj`, not in `TireForge.sln`.

## What it shows — live from `TireForge.ApiProxy` (Stage L)

| Tab | Endpoint | |
|---|---|---|
| Pending Review | `GET /api/queue` | pending diagnoses + the A1/T2/A2 trace + draft work order; **Approve / Reject** → `POST /api/review/{approve,reject}` |
| Work Orders | `GET /api/workorders` | the work-order log + lifecycle; **Close** → `POST /api/workorders/{id}/close` |
| Health Report | `GET /api/status` | 5 seeded machines, bands, latest reading, per-machine anomalies-24h |
| Cost & Governance | `GET /api/cost` | per-agent **call counts** (from the pipeline's records). Token / spend show **—** until the APIM gateway lands (D3/D8) — never mocked. |
| Pipeline Simulator | — | an *illustrative* walkthrough of the pipeline stages. The live pipeline runs server-side; the other tabs are its real output. |

All agent labels are `gpt-5.4` (D1). The mojibake from the prototype is fixed.
Machine identity is whatever `/api/status` returns (the seeded Challenge data),
not the prototype's illustrative roster (D8).

## Run it locally

```bash
# 1. the API (from tireforge/) — CORS so the static page can call it
func start --script-root src/TireForge.ApiProxy --cors "*"      # -> http://localhost:7071

# 2. the dashboard, any static server
npx serve src/TireForge.Dashboard                                # -> http://localhost:3000
```

Then open `http://localhost:3000/?api=http://localhost:7071/api`.

- The `?api=` query param overrides the API base URL. Default is same-origin
  `/<origin>/api` (works when the page is served from the Functions host itself).
- On a connection failure the page shows a red banner naming the URL it tried.

## Deploy

Azure Static Web App via `azure.yaml` / `infra/` (still planned — see `infra/README.md`).
