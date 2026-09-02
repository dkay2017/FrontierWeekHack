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
- `Gating/Gate` — `Evaluate(severity, confidence)` → `GateDecision {Route, Reason}`
  (`confidence < 0.70` OR `severity == Crit` → Review; exactly 0.70 → Auto);
  `Apply(diagnosis)` records it on the row. **[Stage G — done]**
- `Agents/` — agent ports (`IAnomalyDetector` / `IFaultDiagnoser` / `IWorkOrderDrafter`)
  + their output contracts (`AnomalyVerdict` / `FaultVerdict` / `WorkOrderDraft`) +
  `DiagnosisMapper`. Stubs + real impls live in `TireForge.Agents`.
- `Acting/WorkOrderWriter` — the Act step (Stage I): Auto → issue WO via
  `IWorkOrderStore` + `Diagnosis.Status = AutoIssued`; Review → `Pending`, no WO.
  **[Stage I — done]**
- `Pipeline/Pipeline` — `RunAsync(reading)` composes C→D→E→F→G→H→I under one trace
  id; non-anomalous readings stop after D. **[Stage J — done]**
- `Observability/Telemetry` — `ActivitySource` `TireForge.Pipeline`; the pipeline
  emits a root `pipeline.run` span + one child per step, `Diagnosis.TraceId` = the
  W3C trace id. Hosts register with `.WithTracing(t => t.AddSource(Telemetry.SourceName))`.
  **[Tracing stage — done, Challenge 2]**

Covered by `tests/TireForge.Core.Tests` (the Stage A–L checks).
