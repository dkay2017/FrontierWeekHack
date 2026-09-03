# infra

Bicep + `azd` infrastructure for TireForge (cross-cutting).

## What's implemented now

`main.bicep` is a **Bicep port of `factory/challenge-0-setup/deploy.sh`** — it stands
up the same Challenge-0 Foundry stack so it can be reproduced in another
environment / subscription from one deployment:

- Resource group (`foundry-hackathon-rg-<env>` by default)
- Azure AI Foundry account — `Microsoft.CognitiveServices/accounts`, kind `AIServices`,
  system-assigned identity, custom subdomain, `allowProjectManagement`
- Foundry **project** (`factory-project`)
- **Model deployment** — `gpt-5.4` / `2026-03-05`, `GlobalStandard`, capacity 10
- Log Analytics workspace
- Application Insights (workspace-based)
- App Insights ⇄ Foundry **connection** (`appinsights-conn`, ApiKey auth) — the
  Connected-resource that Challenge 2 tracing needs

Outputs mirror the keys `deploy.sh` writes into `factory/.env`
(`FOUNDRY_ENDPOINT`, `PROJECT_CONNECTION_STRING`, `MODEL_DEPLOYMENT_NAME`,
`APPLICATIONINSIGHTS_CONNECTION_STRING`, …).

## Still planned (not yet in Bicep)

- Storage account (ingestion→orchestrator queue, Durable Task hub)
- Function App(s) — Flex Consumption, .NET 8 isolated
- Static Web App (dashboard)
- APIM (Consumption) AI gateway + token policies
- Managed identity + RBAC role assignments

Until those exist, `azure.yaml` service deploys (`azd deploy`) have nowhere to land —
use the plain deployment below.

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
| `main.bicep` | subscription-scoped entry point (creates the RG, calls the module) |
| `modules/foundry.bicep` | the Challenge-0 Foundry stack, resource-group scoped |
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

### `azd`

```bash
azd env new <env>
azd env set AZURE_LOCATION swedencentral
azd provision          # runs main.bicep only
```

## Relationship to `deploy.sh`

`deploy.sh` stays the quick path for the primary hackathon environment (it also
writes `factory/.env` directly). `main.bicep` is the reproducible,
source-controlled definition of the same resources for any other environment.
Keep the two in sync when Challenge-0 resources change.
