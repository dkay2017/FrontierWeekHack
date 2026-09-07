# Runbook — deploy to Azure (`azd up`)

One resource group (`zynara-spike-rg`) holding everything, **next to a
pre-existing Foundry account** (`zynara-foundry-28985` / project `care-approval`
— created by the de-risk spike, holds the 5 agents + the File Search vector
store). `infra/modules/foundry.bicep` *references* that account; it does not
create one.

## Prereqs

- `az login` + `azd auth login` as a subscription **Owner** (role assignments).
- The Foundry account + `gpt-5.4` deployment already exist (spike, or Challenge 0).
- Azure Functions Core Tools installed (`npm i -g azure-functions-core-tools@4`)
  for local testing; not needed for `azd`.

## Provision + deploy

```bash
cd zynara
azd auth login
azd env new zynara-hack
azd env set AZURE_LOCATION      swedencentral
azd env set AZURE_RESOURCE_GROUP zynara-spike-rg
azd env set AZURE_PRINCIPAL_ID  "$(az ad signed-in-user show --query id -o tsv)"
azd env set AGENTSMODE          stub          # flip to foundry once green
azd env set VECTOR_STORE_ID     vs_...        # the File Search vector store id

azd provision                                # infra + the DB seed hook

# deploy ONE service at a time — the Codespace OOMs on 3 parallel dotnet publish
azd deploy orchestrator
azd deploy apiproxy
azd deploy submission
azd deploy dashboard
```

Outputs land in `.azure/zynara-hack/.env` (`APIPROXY_URL`, `DASHBOARD_URL`, …).

## Seed the review queue

The seed hook loads reference data only (criteria / precedents / cohorts), not
assembled cases. Populate the queue by firing the demo scenarios at the live
api-proxy (through the Static Web App URL):

```bash
B="$(grep DASHBOARD_URL .azure/zynara-hack/.env | cut -d'"' -f2)"
for s in demo-ready demo-strengthen demo-review-mandatory demo-contradiction \
         demo-appeal demo-appeal-critic demo-abstain; do
  curl -s -X POST "$B/api/demo/scenarios/$s/run" -o /dev/null -w "$s %{http_code}\n"
done
```

Or use the dashboard's own **"run scenario ▾"** control (masthead, live mode only).

## Flip to the hosted agents

```bash
azd env set AGENTSMODE foundry
azd provision            # updates ZYNARA_AGENTS on the apps
az functionapp restart -n <orchestrator app> -g zynara-spike-rg
```

The apps' `id-reasoning` identity already has **Cognitive Services User** on the
Foundry account, and the 5 agents already exist in the project, so the startup
`EnsureZynaraAgentsAsync` is a no-op verify. Agent calls now hit GPT-5.4;
`chat gpt-5.4` spans appear in App Insights under `AuthOrchestrator` (Challenge 2).

## Shakedown fixes already baked in (D30)

CognitiveServices API `2025-06-01` · name suffix seeded on the RG · Static Web App
in `westeurope` (not offered in the Nordics) · explicit `Newtonsoft.Json`
(Cosmos SDK 3.x runtime need) · **`AZURE_CLIENT_ID`** per app (the code's
`DefaultAzureCredential` can't pick a user-assigned identity without it) ·
deployer principal granted Cosmos Data Contributor for the seed hook · Consumption
Y1 Linux + identity-based `AzureWebJobsStorage` + Blob/Queue/Table roles ·
dashboard `package.json`.

## Known behaviours (not bugs)

- **Easy Auth on the api-proxy.** The Static Web App's *linked backend* feature
  auto-enables App Service Authentication on the api-proxy, so direct calls to
  `func-zynara-apiproxy-*.azurewebsites.net/api/*` return **401** — only requests
  proxied through the SWA (`<swa-host>/api/*`, which the dashboard uses) succeed.
  This is the intended SWA security model. To allow direct access: drop the
  `linkedBackends` resource, set the SWA to **Free**, and point the dashboard at
  the api-proxy with `?api=<apiproxy-url>`.
- **`/api/submit/{id}` is Anonymous** — guarded by `SubmissionService` (only an
  `approve-send` case is ever sent, idempotent). The dashboard reaches it through
  the api-proxy (`POST /api/cases/{id}/submit` → `SUBMISSION_URL`), so it never
  needs a key. Production would front it with a key or the identity boundary.

## Verified end to end (2026-09-07)

`POST /api/requests` → Durable `AuthOrchestrator` → 6 activities + Gate +
`PersistCaseActivity` → case in Cosmos → dashboard queue → `POST
/api/cases/{id}/decision` (ApprovalAuthority + audit) → `POST
/api/cases/{id}/submit` → Submission Adapter reads the payer credential from Key
Vault → `submissions` container. Both `stub` and `foundry` agent modes.

## Tear down

```bash
azd down --purge      # keeps the referenced Foundry account (it's `existing`)
```
