# TireForge

Predictive-maintenance agent for the Factory / Agent-a-Thon scenario.
Sensor telemetry → anomaly detection → Foundry agent reasoning → work-order decision.

## Solution layout

```
tireforge/
├── TireForge.sln
├── global.json                 # pins .NET 8 SDK
├── azure.yaml                  # azd service map
├── infra/                      # Bicep + azd (cross-cutting)
├── src/
│   ├── TireForge.Core/         # domain logic, pure, no cloud (logic Stages A–L)
│   │                           #   Model / Thresholds (T1) / History (T2) / Gate / Pipeline
│   ├── TireForge.Agents/       # Foundry agent clients + prompts + output schemas
│   │                           #   IAgentClient (stub impl + real impl)   [AI Foundry layer]
│   ├── TireForge.Ingestion/    # Sensor Simulator (Timer fn) + queue publisher   [Ingestion]
│   ├── TireForge.Orchestrator/ # Durable Functions hub + activities   [Compute · Hub & Spokes]
│   ├── TireForge.Data/         # Work Order Adapter + SQLite + seed data   [Data]
│   ├── TireForge.ApiProxy/     # HTTP fn: /status /queue /workorders /cost /simulate /decision
│   └── TireForge.Dashboard/    # Static Web App (port of v1.6)   [Experience]
├── eval/
│   └── TireForge.Eval/         # evaluation harness: 4 scenarios, LLM-judge
└── tests/
    ├── TireForge.Core.Tests/   # the Stage A–L checks
    ├── TireForge.Agents.Tests/
    └── TireForge.Data.Tests/
```

## Project references

| Project | References |
|---|---|
| TireForge.Agents | Core |
| TireForge.Data | Core |
| TireForge.Ingestion | Core |
| TireForge.Orchestrator | Core, Agents, Data |
| TireForge.ApiProxy | Core, Data |
| TireForge.Eval | Core, Agents |
| *.Tests | matching src project |

`TireForge.Dashboard` is a static site (no `.csproj`, not in the solution).

## Prerequisites

- .NET 8 SDK (`global.json` pins the version; the devcontainer installs it)
- Azure Functions Core Tools v4 (`func`)
- Azure CLI + azd (for `infra/`)

## Build & test

```bash
dotnet build TireForge.sln
dotnet test  TireForge.sln
```

## Run a Functions app locally

```bash
cd src/TireForge.ApiProxy
func start
```
