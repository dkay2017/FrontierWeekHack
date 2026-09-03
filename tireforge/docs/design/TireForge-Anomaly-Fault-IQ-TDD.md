# TireForge Anomaly-Fault IQ — Technical Design Document

> *Built for the grip. Watching for the slip.*
> Microsoft Agent-a-Thon · Architect Track Submission · Factory Scenario · Deepak Kumar · Original Work

*(Markdown transcription of `TireForge-Anomaly-Fault-IQ-TDD.pdf` for in-repo reference. The
PDF and the architecture SVG are the authoritative artefacts.)*

---

## 1. Problem Statement

TireForge Industries runs a tire manufacturing plant with 5 production machines. Machine
failures are currently caught **reactively** — after a breakdown, not before one.

- **What this costs:** unplanned downtime, reactive maintenance call-outs, and no early
  warning between "machine is fine" and "machine has failed."
- **What's missing:** nobody is continuously watching the machine telemetry, correlating it
  against history, or turning it into an action before it becomes a stoppage.
- **The ask (per the organiser's brief):** build an agentic workflow that watches real-time
  sensor data and turns it into three things — anomaly detection, fault diagnosis, and a
  factory health report.

## 2. Mission & Scope

**Sensor inputs (per brief):** Temperature, Pressure, Vibration, RPM

**Mission (per brief, verbatim):**
- Detects anomalies
- Diagnoses possible faults
- Produces a factory health report

**In scope for this design:**
- End-to-end agentic pipeline: sensor → detection → diagnosis → work order → health report
- Human-in-the-loop review for low-confidence / high-severity cases
- AI Governance: per-model token quota and cost visibility (not just detection accuracy)
- Grounding: every agent verdict cites the record that drove it (reading ID / incident ID)

**Explicitly out of scope (see §8):**
- Production-grade data store (SQLite is a deliberate demo-scope shortcut)
- Downtime-cost / "stop the line now vs. later" impact estimation — planned, not yet designed in
- Adaptive threshold tuning and model fine-tuning — roadmap only

## 3. Solution Overview

TireForge Anomaly-Fault IQ is a multi-agent system on:
- **Microsoft Foundry Agent Service**
- orchestrated by **Azure Durable Functions**
- governed through an **Azure API Management (APIM) AI Gateway**

It replaces manual monitoring with three specialised agents that detect, diagnose, and act —
with a human reviewer as the safety net, not a bottleneck.

- 3 purpose-built agents instead of one generalist — each with a narrow job and a specific tool
- A single mandatory write path (the Work Order Adapter) so no agent ever touches the data
  store or a machine directly
- Governance built in from the start — token quota per model, cost tracking, confidence-gated
  human review

## 4. Technical Architecture

Five layers, left to right along the data path, plus three cross-cutting concerns.

| # | Layer | Components |
|---|-------|-----------|
| 1 | **Ingestion** | Sensor Simulator (Azure Function · Timer Trigger) → Reading Queue (Azure Storage Queue) |
| 2 | **Compute · Hub & Spokes** (Azure Durable Functions) | Reliability Orchestrator (hub) → ThresholdCheck / HistoryMatch / WorkOrderWriter (activity fns / spokes) |
| 3 | **AI Foundry · Agent Service** | AI Gateway (APIM Consumption) → Anomaly Detection (`gpt-4.1-mini`) / Fault Diagnosis (`gpt-4.1`) / Work Order (`gpt-4.1-mini`) agents |
| 4 | **Data** | Work Order Adapter (Azure Function · Data Source Adapter) → SQLite file store (Machines · Readings · History · WorkOrders) |
| 5 | **Experience** | Reviewer (human) → Dashboard (Static Web App) → API Proxy (Azure Function) |

**Cross-cutting (applies to every layer):**
- **Security & Identity** — Managed Identity (no stored secrets; Function App auths to Foundry
  directly), Key Vault (the one exception: model connection key)
- **Observability** — App Insights (one correlated trace ID per reading, across every hop),
  Evaluation Harness (4 scripted scenarios, scored offline), Health Workbook (Azure Resource
  Graph, works with zero traffic)
- **Responsible AI** — confidence-gated oversight (low confidence or high severity → straight
  to Reviewer), bounded authority (read + write WorkOrders only; no tool can touch equipment)
- **AI Governance** — APIM token quota per model, cost metering

## 5. End-to-End Flow

`Emit → Buffer → Trigger → Check → Detect → Match → Diagnose → Gate → Act → Review → Report`

1. **Emit** — Sensor Simulator (timer) generates a synthetic reading (T/P/V/RPM), drops it on
   the Reading Queue.
2. **Buffer** — the queue decouples ingestion from processing so a burst can't overwhelm the
   orchestrator.
3. **Trigger** — a new queue message starts the Reliability Orchestrator (Durable Functions hub).
4. **Check** — orchestrator calls ThresholdCheck (spoke) to test the reading against known
   machine thresholds.
5. **Detect** — orchestrator calls the Anomaly Detection agent through the AI Gateway; agent
   flags anomalous readings and cites the triggering reading ID.
6. **Match** — if anomalous, orchestrator calls HistoryMatch (spoke) for comparable past
   incidents.
7. **Diagnose** — orchestrator calls the Fault Diagnosis agent through the AI Gateway; it names
   the probable fault and cites the matched incident ID.
8. **Gate** — low-confidence or high-severity diagnoses route straight to the human Reviewer.
9. **Act** — the Work Order agent drafts a work order citing the source reading ID;
   WorkOrderWriter (spoke) hands it to the Work Order Adapter — the only path into the data
   layer.
10. **Review** — the Reviewer approves/rejects pending items from the Dashboard; approved items
    flow back through the same Adapter, never around it.
11. **Report** — the Dashboard's Health Report tab (not an agent) compiles machine status,
    anomaly/fault counts, and resolution rate from the same store.

Throughout: every hop carries one correlated trace ID; every model call passes the AI Gateway
(token quota enforced per model); every agent is bounded to read + write WorkOrders only.

## 6. Components, Service by Service

**Ingestion** — Sensor Simulator (Function · Timer Trigger); Reading Queue (Storage Queue).

**Compute · Hub & Spokes (Durable Functions)** — Reliability Orchestrator (hub, sequences all
3 agents and drives every tool call); ThresholdCheck (activity); HistoryMatch (activity);
WorkOrderWriter (activity).

**AI Foundry · Agent Service** — AI Gateway (APIM Consumption: per-model token quota, rate
limiting, cost cap in front of every model call); Anomaly Detection agent (`gpt-4.1-mini`,
tool call); Fault Diagnosis agent (`gpt-4.1`, tool call); Work Order agent (`gpt-4.1-mini`,
tool call).

**Data** — Work Order Adapter (Function · Data Source Adapter; the only path to CMMS/ERP);
SQLite file store (packaged with the Function App; Machines · Readings · History · WorkOrders;
deliberate demo-scope shortcut).

**Experience** — Reviewer (human); Dashboard (Static Web App: Pending Review · Work Orders ·
Cost · Health Report tabs); API Proxy (Function; serves live status + read data).

**Cross-cutting** — Managed Identity; Key Vault (model connection key only); App Insights
(one correlated trace ID per reading); Evaluation Harness (4 scripted scenarios); Health
Workbook (Azure Resource Graph).

## 7. AI Governance — the AI Gateway in Detail

Governance is the concern the design leads with, not an afterthought.

> **Scope note (session 4 — see DECISIONS D3 / D13).** An APIM AI Gateway that
> *enforces* a per-agent token budget is a **production-essential control** and is
> retained here as the reference design. It was **deliberately not built** for this
> submission because: (1) since the agents became *hosted Foundry agents* invoked
> via the Responses API (DECISIONS D9), the `azure-openai-*` policies — written for
> the Chat Completions shape, and blind to the agent's server-side model calls —
> cannot do per-model token metering without bespoke policy work; (2) the **cost
> visibility** the gateway would feed is obtained directly from each agent
> response (`ResponseResult.Usage`) and persisted to an `AgentCalls` metering table
> (DECISIONS D13) — the Dashboard Cost tab shows real tokens + spend without APIM;
> (3) solo build inside the competition window (APIM is priority 3 in §8's own
> ordering). What is lost by descoping is *enforcement* (a 429 on quota), not
> reporting. The rest of this section is the roadmap shape.

- **Tier: APIM Consumption** — true pay-per-call, near-zero cost at hackathon traffic,
  ~5–10 min to provision (vs. 30–45 on Developer).
- **`azure-openai-token-limit` policy** — token-per-minute quota per model, counted from actual
  prompt + completion tokens (not request count). Returns 429 once a model's budget is exceeded.
- **Differentiated caps** — tighter ceiling on the diagnosis agent, looser on the
  detection / work-order agents. Governance that reflects real cost profile, not a flat limit.
- **`azure-openai-emit-token-metric` policy** — emits real per-model token usage to App Insights;
  a second feed for the same metering the `AgentCalls` table now provides.
- **Why it matters** — AI Governance as an *enforced* control, capped, priced, and reported.

## 8. Deployment & Scope Decisions

Deliberate trade-offs for a solo, part-time build inside the competition window.

- **SQLite over Table/SQL** — packaged with the Function App for simplicity. Known limitation:
  fragile under concurrent writes on Consumption/Flex plans; production → Azure Table Storage or
  Azure SQL.
- **APIM Consumption over Developer/Standard** — chosen to avoid a fixed idle cost;
  Developer/Basic/Standard bill hourly whether called or not, Consumption only on use.
- **Built on the official scaffold** — extends the `microsoft/FrontierWeekHack` Factory scenario
  lab rather than a blank resource group.
- **Build priority if time runs short (agreed order):**
  1. Sensor → Queue → Orchestrator → 3 Agents → Adapter → Data (the core mission)
  2. Dashboard + Reviewer loop running on real data
  3. APIM AI Governance layer
  4. Evaluation Harness + Health Workbook

## 9. Out of Scope · Future Roadmap

- **Downtime-cost estimation** — "cost of stopping the line now vs. later," pulled from an
  earlier related project; not yet designed into this architecture.
- **Adaptive threshold tuning** — thresholds adjusted from reviewer feedback over time.
- **Periodic model fine-tuning** — roadmap tier, not planned for this submission.
