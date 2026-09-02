# Design docs

The independent design for **TireForge Anomaly-Fault IQ** (Deepak Kumar, Architect Track
submission for the MS Agent-a-Thon Factory scenario). These drive the `tireforge/` implementation.

| File | What it is |
|---|---|
| `TireForge-Anomaly-Fault-IQ-TDD.md` | Technical Design Document — problem, architecture, end-to-end flow, AI governance, scope decisions. Markdown transcription of the source PDF. |
| `TireForge-Anomaly-Fault-IQ-Build_Plan.html` | Build Logic Plan — Stages A–M, baby-steps sequence (pure logic first, AI stubbed, Adapter-only-write from step 1), each step with a check. |
| `TireForge-Anomaly-Fault-IQ-Architecture_Design.svg` | Component-level architecture diagram. Same diagram appears on p.5 of the TDD PDF. |

## How the design maps to the solution

| Design layer / stage | Project |
|---|---|
| Ingestion: Sensor Simulator + Reading Queue | `src/TireForge.Ingestion` |
| Compute hub & spokes: Reliability Orchestrator + ThresholdCheck / HistoryMatch / WorkOrderWriter | `src/TireForge.Orchestrator` |
| AI Foundry: Anomaly Detection / Fault Diagnosis / Work Order agents | `src/TireForge.Agents` |
| Data: Work Order Adapter + SQLite (Machines · Readings · History · Diagnoses · WorkOrders) | `src/TireForge.Data` |
| Experience: Reviewer → Dashboard → API Proxy (`/status` `/queue` `/workorders` `/cost`) | `src/TireForge.Dashboard` + `src/TireForge.ApiProxy` |
| Build Plan Stages A–L (pure, no cloud, all testable): Model / T1 / T2 / Gate / Pipeline | `src/TireForge.Core` |
| Evaluation Harness (4 scripted scenarios) | `eval/TireForge.Eval` |
| Cross-cutting: APIM AI Gateway, Key Vault, Managed Identity, App Insights, Health Workbook | `infra/` |
