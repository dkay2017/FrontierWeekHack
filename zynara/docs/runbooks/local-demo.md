# Runbook — run the whole thing locally (no Azure)

The dashboard talks to a real `Zynara.ApiProxy` running on your machine, backed by
the in-memory `DemoWorld` (no Cosmos, no Foundry). Everything the demo shows — the
review queue, the evidence card, the precedent panel, the Critic, the Gate, the
approval-authority check, the audit trail — is live output from the pipeline.

## Prereqs (once)

```bash
npm install -g azure-functions-core-tools@4        # the Functions host
dotnet build zynara/Zynara.sln
```

## Run (two terminals)

```bash
# 1 — the API (from zynara/src/Zynara.ApiProxy)
func start --port 7071 --cors "*"
#    it pre-runs the 7 demo scenarios on startup, so the queue is populated

# 2 — the dashboard (from zynara/src/Zynara.Dashboard)
npx serve .                                        # -> http://localhost:3000
```

Open **`http://localhost:3000/?api=http://localhost:7071`**.

- The queue, recovery, benchmark and profile panels all load from the API.
- The masthead source note shows `live · http://localhost:7071`.
- Any fetch failure falls back to the inlined snapshot (red note on the queue cap).

## What works live

| Action | Endpoint | Result |
|---|---|---|
| Load the queue | `GET /api/cases` + `/api/cases/{id}` | real `CaseView`s |
| Recovery / Impact tabs | `GET /api/recovery` · `/api/benchmark` | real figures |
| Profile modal | `GET /api/profiles` | UK / US regulatory profiles |
| Re-run a scenario | `POST /api/demo/scenarios/{id}/run` | the pipeline executes again |
| Approve / reject | `POST /api/cases/{id}/decision` (`X-Reviewer-Role` header) | `ApprovalAuthority` enforced; the refusal or the decision is appended to the audit trail |

## To point at real Cosmos instead

Set `COSMOS_ENDPOINT` (+ `az login` with the data-plane role) before `func start`.
The API then reads/writes the `careapproval` database instead of `DemoWorld`.

## The hosted agents

`func start` runs the deterministic stubs. For the hosted Foundry agents set
`ZYNARA_AGENTS=foundry` + `PROJECT_ENDPOINT` + `VECTOR_STORE_ID` (see
`foundry-spike.md`) — the pipeline then calls GPT-5.4 for the reasoning steps.
