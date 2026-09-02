# TireForge.Agents

The **AI Foundry** layer: agent clients, prompts, and output schemas. Each agent
has a deterministic **stub** (Build Plan Stages D/F/H) and — from Stage M — a real
Foundry implementation behind the same interface. The pipeline is identical either
way.

| Agent | Interface / output | Stub | Status |
|---|---|---|---|
| A1 Anomaly Detection | `Anomaly/IAnomalyDetector` → `AnomalyVerdict {IsAnomaly, Text, Cites}` | `StubAnomalyDetector` — anomaly iff any sensor out of band (from T1) | **Stage D — done** |
| A2 Fault Diagnosis | `Diagnosis/IFaultDiagnoser` → `FaultVerdict {Fault, Severity, Confidence, Text, Cites}` (`.Validate()`) | `StubFaultDiagnoser` — fault from exact history else the Challenge 1 rubric; tuned confidence | **Stage F — done** |
| A3 Work Order | `WorkOrders/IWorkOrderDrafter` → `{machine, fault, severity, reading_id, action}` | — | Stage H |

`Diagnosis/DiagnosisMapper.ToEntity(...)` assembles the persistable `Diagnosis`
row (full detect/match/diagnose trace); `Core.Gating.Gate.Apply` then fills
`Route` / `GateReason`.
| — real impls | Azure AI Foundry / Agents SDK, `gpt-5.4` | — | Stage M |

References `TireForge.Core`.
