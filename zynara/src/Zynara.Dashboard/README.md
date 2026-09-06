# Zynara.Dashboard

The reviewer workspace (Static Web App) — evidence-first, not approve-button-first
(evaluator P1-1 / P1-2). One `index.html`, no build step.

## Run

- **Offline (default):** open `index.html` (or serve the folder). It loads
  `demo-cases.json` — five pre-computed `CaseView`s covering every Gate route.
- **Live API:** `index.html?api=http://localhost:7071` — fetches from
  `Zynara.ApiProxy` (`/api/cases`, `/api/cases/{id}`, `/api/demo/scenarios`,
  `/api/demo/scenarios/{id}/run`, `/api/cases/{id}/decision`).

`demo-cases.json` is generated from `Zynara.Core.Demo` — regenerate it whenever
the projection or the demo scenarios change.

## What the card shows

WHY → per-criterion EVIDENCE (the quoted clinical statement + source) → the
PRECEDENT PANEL (top-3 comparable cases, match %, won-on-appeal / initially-denied
badges, matched-fact chips, clauses, "drove the recommendation") → the CRITIC
verdict and its flags → CONFLICTS (never resolved silently) → the deterministic
GATE routing with its decision model → the draft → the HUMAN CONTROL bar
(approve · edit · request evidence · reject).

## Tabs

- **Review Queue** — the cases + the review card.
- **Early Warnings** — the deterministic monitors (advisory only).
- **Cost** — per-case inference cost (populated when the Foundry agents run).
