# Zynara.Dashboard

The reviewer workspace (Static Web App) — evidence-first, not approve-button-first
(evaluator P1-1 / P1-2). One `index.html`, no build step.

## Run

- **Offline (default):** open `index.html` (or serve the folder). The demo
  dataset is inlined (`<script id="demo-data">`) so it renders with no server —
  this is also the version published as an artifact for review.
- **Live API:** `index.html?api=https://<apiproxy-host>` — pulls real cases from
  `Zynara.ApiProxy` (`/api/cases`, `/api/cases/{id}`, `/api/recovery`,
  `/api/benchmark`, `/api/profiles`, `/api/demo/scenarios`) and re-renders. Any
  fetch error falls back to the inlined snapshot.

The inlined snapshot and `demo-cases.json` are generated from `Zynara.Core.Demo`
by `tools/Zynara.DemoDump` — regenerate both whenever the projection or the demo
scenarios change (`dotnet run --project tools/Zynara.DemoDump -- src/Zynara.Dashboard/demo-cases.json`,
then re-inline into `index.html`).

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
