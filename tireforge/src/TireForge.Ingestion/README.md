# TireForge.Ingestion

**Ingestion** layer. Azure Functions (isolated worker, .NET 8). References
`TireForge.Core` + `TireForge.Data` (reads the machine roster from the same store
the pipeline uses).

## Functions

| Function | Trigger | What it does |
|---|---|---|
| `SensorSimulator` | Timer, every 5 min | one `ReadingFactory` reading per seeded machine (≈74 % normal / 19 % warn / 7 % crit) → `readings` queue |
| `EmitReading` | HTTP `POST /api/emit/{machineId}/{mode?}` | one reading on demand (`mode` = normal / warn / crit, default warn) → `readings` queue; returns 202 + the reading id. Challenge 4 "HTTP trigger: on-demand endpoint". |

`Reading.Machine` is nulled before the message is serialised — the queue carries
a flat reading. `TireForge.Orchestrator` consumes `readings`.

## Run locally

```bash
cp local.settings.sample.json local.settings.json      # then edit if needed
# Azurite (storage emulator) must be running for the queue + timer
func start
```

`TIREFORGE_DB` in the sample points at `../../tireforge.db` so Ingestion, the
Orchestrator and the ApiProxy all share one SQLite file for the local demo.
