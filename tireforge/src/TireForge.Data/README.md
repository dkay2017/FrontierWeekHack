# TireForge.Data

**Data** layer — EF Core over SQLite (Decision D4). Implements the Core
persistence ports; the Work Order Adapter is the sole write path into `WorkOrders`
(invariant 1.1).

## Contents

| Piece | File(s) |
|---|---|
| DbContext — 5-table schema | `TireForgeDbContext.cs` |
| Store implementations | `Repositories/Stores.cs` (`MachineStore`, `ReadingStore`, `HistoryStore`, `DiagnosisStore`, `WorkOrderStore`) |
| Seed — 5 machines + bands + 1 snapshot reading each + ~8 history incidents | `Seed/DbSeeder.cs`, `Seed/SensorDataFile.cs` |
| Sample data (embedded) | `Seed/sensor_data.json` — verbatim copy of `factory/challenge-1-build/sensor_data.json` |
| Migrations | `Migrations/` (`InitialCreate` = Stage-A DDL) |
| Design-time factory | `TireForgeDbContextFactory.cs` |

## Schema (Build Plan Stage A / §15)

`Machines` · `Readings` (+ `IsAnomaly`) · `History` · `Diagnoses` (pending trace +
gate reason) · `WorkOrders` (+ rejected audit rows).

Sensor bands are owned columns on `Machines` (`Temperature_Min/Max/Unit`, …).
`DateTimeOffset` is stored via `DateTimeOffsetToBinaryConverter` so SQLite can
order by it.

## Working with migrations

`dotnet ef` needs the SDK's runtime on the path:

```bash
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"

dotnet-ef migrations add <Name> --project src/TireForge.Data --startup-project src/TireForge.Data --output-dir Migrations
dotnet-ef database update  --project src/TireForge.Data --startup-project src/TireForge.Data
```

Override the connection string with `TIREFORGE_DB` (default `Data Source=tireforge.db`).

## Tests

`tests/TireForge.Data.Tests` — in-memory SQLite (`TestDb`), covers the Stage-A
checks (seed counts, band values, round-trips, history match, pending filter).
