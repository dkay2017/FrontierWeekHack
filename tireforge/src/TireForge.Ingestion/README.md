# TireForge.Ingestion

**Ingestion** layer. Azure Functions (isolated worker, .NET 8).

- Sensor Simulator — Timer-triggered function generating synthetic telemetry
- Queue publisher — pushes readings onto the storage queue consumed by
  `TireForge.Orchestrator`

References `TireForge.Core`.
