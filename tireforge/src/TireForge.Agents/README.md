# TireForge.Agents

The **AI Foundry** layer: agent implementations, prompts, and (Stage M) the real
Foundry clients. The **ports and output contracts live in `TireForge.Core.Agents`**
so the pure `Core.Pipeline` can call them; this project supplies the behaviour.

Everything here is in the flat `TireForge.Agents` namespace (folders are just
organisation) — a sub-namespace called `Diagnosis` would collide with
`Core.Model.Diagnosis`.

| Agent | Port (in Core) / output | Stub here | Status |
|---|---|---|---|
| A1 Anomaly Detection | `IAnomalyDetector` → `AnomalyVerdict {IsAnomaly, Text, Cites}` | `StubAnomalyDetector` — anomaly iff any sensor out of band (from T1); `ApplyTo` writes `IsAnomaly` back | **Stage D — done** |
| A2 Fault Diagnosis | `IFaultDiagnoser` → `FaultVerdict {Fault, Severity, Confidence, Text, Cites}` (`.Validate()`) | `StubFaultDiagnoser` — fault from exact history else the Challenge 1 rubric; tuned confidence; severity escalates to a matched Crit incident | **Stage F — done** |
| A3 Work Order | `IWorkOrderDrafter` → `WorkOrderDraft {MachineId, Fault, Severity, ReadingId, ActionText}` | `StubWorkOrderDrafter` — templates the action from the diagnosis, urgency by severity, cites the reading | **Stage H — done** |
| — real impls | Azure AI Foundry / Agents SDK, `gpt-5.4` | — | Stage M |

`Core.Agents.DiagnosisMapper.ToEntity(...)` assembles the persistable `Diagnosis`
row (full detect/match/diagnose trace); `Core.Gating.Gate.Apply` then fills
`Route` / `GateReason`; `Core.Acting.WorkOrderWriter` issues or holds.

References `TireForge.Core`.
