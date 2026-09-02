# TireForge.Agents

The **AI Foundry** layer: agent clients, prompts, and output schemas. Each agent
has a deterministic **stub** (Build Plan Stages D/F/H) and — from Stage M — a real
Foundry implementation behind the same interface. The pipeline is identical either
way.

| Agent | Interface / output | Stub | Status |
|---|---|---|---|
| A1 Anomaly Detection | `Anomaly/IAnomalyDetector` → `AnomalyVerdict {IsAnomaly, Text, Cites}` | `StubAnomalyDetector` — anomaly iff any sensor out of band (from T1) | **Stage D — done** |
| A2 Fault Diagnosis | `Diagnosis/IFaultDiagnoser` → `{fault, severity, confidence, text, cites}` | — | Stage F |
| A3 Work Order | `WorkOrders/IWorkOrderDrafter` → `{machine, fault, severity, reading_id, action}` | — | Stage H |
| — real impls | Azure AI Foundry / Agents SDK, `gpt-5.4` | — | Stage M |

References `TireForge.Core`.
