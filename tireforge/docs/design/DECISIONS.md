# Implementation decisions

Deltas from the TDD / Build Plan, agreed 2026-09-02. The TDD stays as the design of record;
this file is what we actually build. Guiding principle: **get one end-to-end run working first,
refine after.**

## D1 — One model: `gpt-5.4` for all three agents

The TDD specifies `gpt-4.1` (diagnosis) + `gpt-4.1-mini` (detection, work order). Challenge 0
deployed **`gpt-5.4`** — the only model available on this Foundry account (`factory-project`,
swedencentral, GlobalStandard).

**Decision:** all three agents (`IAgentClient` real impl) use the `gpt-5.4` deployment.
`MODEL_DEPLOYMENT_NAME=gpt-5.4` in `factory/.env`.

**Impact on the AI-governance story (D3):** "differentiated caps" become **per-agent** (via APIM
products / subscription keys), not per-model. Still a demonstrable, enforced control. The
per-model `gpt-4.1` vs `gpt-4.1-mini` split moves to the roadmap section.

## D2 — Durable Functions: pipeline runs inside one activity for v1

The Build Plan composes `run_pipeline = C→D→E→F→G→H→I` as one function. A C# Durable
**orchestrator** can't do IO directly (replay determinism), so the pure `Core.Pipeline` can't be
the orchestrator verbatim.

**Decision:** v1 — the queue-triggered orchestrator calls **one activity** that runs
`Core.Pipeline.Run(reading)` end to end (DB + `IAgentClient` calls inside the activity). Simple,
correct, gets us to end-to-end fastest.

**Refinement (later, if time):** split into step-level activities
(`ThresholdCheck` / `Detect` / `HistoryMatch` / `Diagnose` / `Act`) sequenced by the orchestrator —
this is the "hub & spokes" of the diagram and gives per-step retry + durability. `Core` logic is
identical either way; only the `TireForge.Orchestrator` wiring changes.

## D3 — APIM AI Gateway: built last

APIM is the AI Governance gate and sits in front of every model call (TDD §7). It is **priority 3**
in the TDD's own build order.

**Decision:** agents call the model deployment directly
(`https://foundry-hack-3e97ae19.cognitiveservices.azure.com/openai/deployments/gpt-5.4/...`,
verified working with Entra ID auth) until the core pipeline + dashboard are done. Then insert
APIM in front — config change to the agent client's base URL, plus the
`azure-openai-token-limit` and `azure-openai-emit-token-metric` policies.

**Open spike before D3:** confirm APIM can proxy the Foundry model-deployment path with the
`azure-openai-*` policies (they target the AOAI data plane; Foundry Agent Service may route
differently). If not, fall back to per-agent APIM products without the token-metric policy and
feed the Cost tab from App Insights GenAI trace token counts instead.

## D4 — Data store: EF Core, SQLite now, swap provider if it bites

The domain is relational — 5 tables with FKs, joins for health metrics and `/queue`, a WorkOrder
lifecycle state machine. SQLite's real risk (concurrent writes on Flex/Consumption) is largely
mitigated by the **Work Order Adapter being the sole writer** (writes are serialized through one
code path).

**Decision:**
- `TireForge.Data` uses **EF Core** with repository interfaces (`IMachineStore`, `IReadingStore`,
  `IHistoryStore`, `IDiagnosisStore`, `IWorkOrderStore`).
- **Local / tests / Stages A–L / demo:** SQLite (file, or in-memory for tests). Zero infra,
  satisfies "no cloud" for the pure-logic stages. EF migrations = the Stage-A schema DDL.
- **Deploy target if SQLite friction appears:** Azure SQL **serverless** (auto-pause, ~$0 idle) —
  same EF Core code, swap the provider + connection string.
- **Cosmos DB** stays an option if we later want JSON-native / globally-distributed, but it would
  mean hand-rolling the joins and FK integrity this design leans on — not the low-friction path
  for this data shape.

## D5 — Schema: 5 tables (Build Plan §15)

`Machines`, `Readings` (+ `is_anomaly`), `History`, `Diagnoses` (pending trace + gate reason),
`WorkOrders` (+ audit rows for rejects). Confirmed — `Diagnoses` is in, not the TDD's 4-table list.
