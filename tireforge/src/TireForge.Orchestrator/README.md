# TireForge.Orchestrator

**Compute — Hub &amp; Spokes.** Azure Functions + Durable Functions (isolated
worker, .NET 8). References `TireForge.Core`, `TireForge.Agents`, `TireForge.Data`.

## Flow

```
readings queue ──▶ PipelineStarter (queue trigger)
                        │  instanceId = reading.Id  → duplicate delivery is a no-op
                        ▼
                 PipelineOrchestrator (orchestration trigger — deterministic)
                        ▼
                 RunPipeline (activity — the only place IO happens)
                        ▼
                 Core.Pipeline.RunAsync  →  PipelineRunSummary
```

**Decision D2:** v1 runs the whole `Core.Pipeline` (C→D→E→F→G→H→I) in **one**
activity. Splitting into step-level activities (per-step retry, the durable review
gate) is a later refinement of this project only — `Core` is unchanged.

`Program.cs` wires `AddTireForgeData` + `AddTireForgeAgents`
(`TIREFORGE_AGENTS=stub|foundry`) + `AddScoped<Pipeline>()`, and migrates/seeds on
startup for the local demo.

## Run locally

```bash
cp local.settings.sample.json local.settings.json      # then edit if needed
func start                                              # needs Azurite running
```

Then feed it a reading — either let `TireForge.Ingestion`'s timer fire, or:

```bash
curl -X POST http://localhost:7072/api/emit/CP-003/crit    # via Ingestion
```

Watch the orchestration in the Durable instance store / App Insights; the result
shows up in the dashboard (via `TireForge.ApiProxy`, shared SQLite file).

## Tests

`tests/TireForge.Orchestrator.Tests` — the `Program.cs` DI wiring resolves, and
the `RunPipeline` activity drives the pipeline end to end over a seeded in-memory
DB (crit → review, normal → stop, confident warn → auto WO).
