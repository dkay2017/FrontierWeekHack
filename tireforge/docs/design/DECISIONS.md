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

---

## D6 — Correlation: W3C `Activity`, one trace per reading

The TDD's "one correlated trace ID per reading, across every hop" (invariant, §4) and
Challenge 2 (GenAI tracing) are the same requirement.

**Decision:** the pipeline's trace id is a real **W3C trace context** via
`System.Diagnostics.ActivitySource` (`TireForge.Pipeline`), not a bare GUID. One root
activity (`pipeline.run`) per `Pipeline.RunAsync`, one child activity per step
(`t1.threshold_check`, `a1.detect`, `t2.history_match`, `a2.diagnose`, `gate`,
`a3.draft`, `act`), each tagged with `tireforge.reading_id` / `machine_id` / `severity`
/ `confidence` / `gate.route`. `Diagnosis.TraceId` = the root's `TraceId` (32-hex), so
the stored id equals App Insights `operation_Id`.

**Export:** the Functions hosts already wire `Azure.Monitor.OpenTelemetry.Exporter`
(scaffold). Each `Program.cs` adds `.WithTracing(t => t.AddSource("TireForge.Pipeline"))`
so our spans reach App Insights (connection string from `factory/.env`). At Stage M the
Foundry SDK's `gen_ai.*` spans nest under our activity for free (also feeds the Cost tab).

**Complexity: low (~4/10)** — `ActivitySource` is BCL, exporter is scaffolded. Own stage,
right after the pipeline.

## D7 — Persist the work-order draft on every route (A1)

The reviewer drawer shows "Draft work order · prepared, not issued". The Review route
writes no `WorkOrders` row, so the drafted action text had nowhere to live.

**Decision:** add `Diagnosis.DraftActionText`; `WorkOrderWriter.ActAsync` records the A3
draft on the diagnosis on **both** routes (before the auto/review branch). Small: one
column + migration + one line.

## D8 — Dashboard scope + APIM timebox

- **Dashboard:** the v1.6 prototype (`docs/design/…-Dashboard_Prototype.html`) is
  committed as **mock data**. The real `TireForge.Dashboard` is a port that binds the
  mock `api` object to `TireForge.ApiProxy` and the **seeded Challenge data** (the
  prototype's machine roster / numbers are illustrative — not reconciled).
- **Dropped** (complexity without payoff for a judge):
  - Simulator "drift" / "stall" scenarios → cut the sim to `normal / warn / crit`
    (matches `ReadingFactory`; trend/rolling-window is TDD §9 roadmap).
  - Gateway 429-backoff event log as live narrative → keep as a static "what
    enforcement looks like" card.
  - `WorkOrderStatus.Draft` lifecycle state → the draft lives on the `Diagnosis` (D7).
- **APIM (D3):** timebox the feasibility spike to ~1 h. If APIM can't cleanly proxy the
  Foundry model path, **APIM moves to the roadmap** — keep the TDD governance section,
  keep the Cost tab as illustrative, cite App Insights GenAI token spans (Stage M) as
  the metering path.
- Cost tab shows real numbers only once Stage M agents emit token spans; until then it
  is labelled illustrative (invariant 1.5 — never present mocked figures as real).

## Revised build sequence (post-Stage J)

1. **A1** — `Diagnosis.DraftActionText` (D7).
2. **Tracing stage** — `Activity`-based correlation + host export (D6) = Challenge 2.
3. **Stage K** — reviewer approve / reject / close.
4. **Stage L** — read models `/status /queue /workorders /cost` + health metrics + `TireForge.ApiProxy`.
5. **Dashboard port** — real `fetch`, `gpt-5.4` labels, mojibake fix, sim cut to normal/warn/crit.
6. **Ingestion + Orchestrator wiring** — timer → queue → Durable → `Pipeline.RunAsync` = Challenge 4 shape.
7. **Stage M** — real `gpt-5.4` agents, one at a time = Challenge 1 passed for real.
8. **APIM** — only if the D3 spike passes and time remains.
