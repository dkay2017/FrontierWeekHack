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
- `Sensing/ReadingFactory` — synthesises a `Reading` (normal/warn/crit), injectable
  clock + RNG. **[Stage B — done]**
- `Thresholds/ThresholdCheck` (T1) — `Evaluate(reading, machine)` → `ThresholdReport`
  (per-sensor status + deviation%, seeded `Severity`, citing trace line). C# port of
  Challenge 1's `check_thresholds`. **[Stage C — done]**
- `History/FaultSignature` + `History/HistoryMatch` (T2) — canonical
  `sensor-high/low` signature from T1, exact + token-overlap match against
  `IHistoryStore`, citing `T2 …` trace. **[Stage E — done]**
- `Gate` — `confidence < 0.70` OR `severity == Crit` → review. *[Stage G]*
- `Pipeline` — orchestrates C→D→E→F→G→H→I under one trace id. *[Stage J]*

Covered by `tests/TireForge.Core.Tests` (the Stage A–L checks).
