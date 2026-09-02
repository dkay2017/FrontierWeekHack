# TireForge.Core

Domain logic — **pure, no cloud dependencies**. Implements logic Stages A–L.

Building blocks:

- `Model/` — domain entities + enums: `Machine` (+ `SensorBand`), `Reading`
  (+ `IsAnomaly`), `HistoryIncident`, `Diagnosis`, `WorkOrder`; `Severity`,
  `SensorKind`, `ReadingMode`, `GateRoute`, `DiagnosisStatus`, `WorkOrderStatus`.
  **[Stage A — done]**
- `Abstractions/` — persistence ports (`IMachineStore`, `IReadingStore`,
  `IHistoryStore`, `IDiagnosisStore`, `IWorkOrderStore`), implemented in
  `TireForge.Data`. **[Stage A — done]**
- `Thresholds` (T1) — static threshold evaluation. *[Stage C]*
- `History` (T2) — fault-signature match against `HistoryIncident`. *[Stage E]*
- `Gate` — `confidence < 0.70` OR `severity == Crit` → review. *[Stage G]*
- `Pipeline` — orchestrates C→D→E→F→G→H→I under one trace id. *[Stage J]*

Covered by `tests/TireForge.Core.Tests` (the Stage A–L checks).
