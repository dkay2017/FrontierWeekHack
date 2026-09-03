# infra

Bicep + `azd` infrastructure for TireForge (cross-cutting). `az bicep build main.bicep`
compiles clean; not yet test-deployed.

## Layer 1 — Foundry stack (`modules/foundry.bicep`)

Port of `factory/challenge-0-setup/deploy.sh` — reproduces the Challenge-0 stack:

- Resource group (`foundry-hackathon-rg-<env>`)
- AI Foundry account (`Microsoft.CognitiveServices/accounts`, kind `AIServices`,
  system-assigned identity, custom subdomain, `allowProjectManagement`)
- Foundry **project** (`factory-project`) + **model deployment** (`gpt-5.4` /
  `2026-03-05`, `GlobalStandard`, capacity 10)
- Log Analytics + Application Insights (workspace-based)
- App Insights ⇄ Foundry **connection** (`appinsights-conn`) — Challenge 2 tracing

Outputs mirror the `factory/.env` keys.

## Layer 2 — Compute (`modules/apps.bicep` + `modules/data.bicep`) — Challenge 4

The event-driven Functions architecture ("Option 4"). Toggle with `deployCompute`
/ `deployDatabase` (default `true`).

- **Storage account** — Functions host + Durable Task hub + the `readings` queue.
  `allowSharedKeyAccess: false` — **identity-only** (`AzureWebJobsStorage__accountName`).
- **Flex Consumption plan** (`FC1`) + **three Function Apps**, .NET 8 isolated,
  system-assigned identity, App Insights wired, `azd-service-name` tags:
  `tireforge-ingestion` / `-orchestrator` / `-apiproxy` (CORS `*`).
- **Azure SQL serverless** (`GP_S_Gen5_1`, auto-pause 1 h, 0.5–1 vCore) — Decision
  D4's deploy target for `TireForge.Data`. Entra-only auth; connection string
  (`Authentication=Active Directory Default`) flows to all three apps as `TIREFORGE_DB`.
- **Static Web App** (Free) for the dashboard.
- **Role assignments** — each Function identity gets Storage Blob Data Owner +
  Queue Data Contributor + Table Data Contributor; the Orchestrator identity also
  gets **Cognitive Services User** on the Foundry account (so `DefaultAzureCredential`
  can invoke the agents).

### The one code task before a live `azd up` persists

`TireForge.Data` still uses the **SQLite** EF provider. The apps deploy with
`TIREFORGE_SKIP_DB_INIT=true` and an Azure SQL `TIREFORGE_DB`. Remaining:

1. Add `Microsoft.EntityFrameworkCore.SqlServer`; make `AddTireForgeData` pick the
   provider from the connection string (`Data Source=` → SQLite, else SqlServer).
2. Generate a **SqlServer migrations set** (`dotnet ef migrations add … --context …`
   with the SqlServer provider) — SQLite and SqlServer column types differ.
3. An `azd` `postprovision` hook (or a one-off job) that runs `dotnet ef database
   update` against the provisioned server, then seeds.

Until then the compute infra is provisioned but the apps can't complete a run.

## Still not in Bicep

- APIM (Consumption) AI gateway + token policies — Decision D3, gated on the spike.

## Not Bicep — by design

The **three Foundry agents** are **not** infrastructure — runtime SDK resources
created by `tools/TireForge.AgentTool provision` (D9 / D11 rung 0). Their
`check_thresholds` tool runs in-process against `Core.ThresholdCheck`. Bicep stands
up the account / project / model / App Insights they run on; a `postprovision` hook
runs the provisioner.

## Not Bicep — by design

The **three Foundry agents** (`anomaly-detection-agent` / `fault-diagnosis-agent` /
`work-order-agent`) are **not** infrastructure. They are runtime resources created
via the SDK — `tools/TireForge.AgentTool provision` (Decisions D9 / D11 rung 0,
"create once, reuse forever" — matches `factory/challenge-*/agents.py`). Their
`check_thresholds` tool runs in-process against `Core.ThresholdCheck`, so it needs
no infra either. Bicep stands up the *account + project + model deployment + App
Insights* the agents run on; the agents themselves are provisioned after deploy.

## Files

| File | Purpose |
|---|---|
| `main.bicep` | subscription-scoped entry point (RG + module calls + outputs) |
| `modules/foundry.bicep` | Layer 1 — the Challenge-0 Foundry stack |
| `modules/apps.bicep` | Layer 2 — storage, Flex plan, 3 Function Apps, SWA, RBAC |
| `modules/data.bicep` | Layer 2 — Azure SQL serverless (D4) |
| `main.parameters.json` | parameter values / azd env-var bindings |

## Deploy to a new environment

### Plain `az` (recommended today)

```bash
az login
az account set --subscription <SUBSCRIPTION_ID>

az deployment sub create \
  --location swedencentral \
  --template-file main.bicep \
  --parameters environmentName=hack2 location=swedencentral

# pull the outputs into factory/.env style values
az deployment sub show --name main \
  --query properties.outputs -o json
```

Override any default (model, names, region) with extra `--parameters key=value`
pairs — see the `param` list at the top of `main.bicep`.

Add `--parameters deployCompute=false` to stand up only Layer 1.

### `azd`

```bash
azd env new <env>
azd env set AZURE_LOCATION swedencentral
azd up                 # provision (both layers) + deploy the 4 services
# or: azd provision    # infra only
```

`azure.yaml` maps the four services (`ingestion` / `orchestrator` / `apiproxy` /
`dashboard`) onto the `azd-service-name` tags in `apps.bicep`.

## Relationship to `deploy.sh`

`deploy.sh` stays the quick path for the primary hackathon environment (it also
writes `factory/.env` directly). `main.bicep` is the reproducible,
source-controlled definition of the same resources for any other environment.
Keep the two in sync when Challenge-0 resources change.
