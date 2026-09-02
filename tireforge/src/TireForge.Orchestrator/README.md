# TireForge.Orchestrator

**Compute — Hub & Spokes.** Azure Functions + Durable Functions (isolated worker, .NET 8).

- Queue-triggered starter → durable orchestration
- Activities: run Core pipeline, call `IAgentClient`, persist via `TireForge.Data`
- Fan-out / fan-in across assets

References `TireForge.Core`, `TireForge.Agents`, `TireForge.Data`.
