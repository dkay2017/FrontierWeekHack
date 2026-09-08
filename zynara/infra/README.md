# infra/ — Care Approval IQ

Bicep for the Azure footprint (TDD §12). **Skeleton — not deployed to a live
subscription.** `az bicep build infra/main.bicep` is clean; `azd up` would
provision it.

## What it does — managed-identity first (TDD §7)

| Component | Identity | Gets | Deliberately does NOT get |
|---|---|---|---|
| workflow host, api-proxy | `id-zynara-reasoning` | Foundry inference (Cognitive Services User) · Cosmos data-plane · Blob read · App Insights | the payer-integration secret |
| **Submission Adapter** | `id-zynara-submission` | the payer-integration secret (Key Vault Secrets User) · Cosmos write | **any model / Foundry access** — no `PROJECT_ENDPOINT` |
| dashboard | `id-zynara-dashboard` | — (static content) | everything |

No stored connection strings: Cosmos and Storage have `disableLocalAuth` /
`allowSharedKeyAccess: false`; the one residual secret lives in Key Vault,
readable by the Submission Adapter's identity alone.

## Not in this build — production hardening (TDD §7.1)

Private networking (VNet, private endpoints, NSGs, private DNS), the enforced
**network** boundary between the reasoning plane and payer connectivity, WAF /
Front Door, multi-region DR. **Named, not built** — the identity split above is
the substance; the network isolation is the belt-and-braces a production
deployment adds.

## Modules

| File | What |
|---|---|
| `modules/identity.bicep` | the three user-assigned managed identities |
| `modules/foundry.bicep` | AI Services account + project + model + Log Analytics + App Insights; Cognitive Services User for the reasoning identity only |
| `modules/data.bicep` | Cosmos serverless (containers: requests · submissions · outcomes · caseAudit · agentCalls · denialCohorts) + Blob corpus, RBAC + local-auth off |
| `modules/keyvault.bicep` | the payer-integration secret, `Key Vault Secrets User` for `id-submission` only |
| `modules/apps.bicep` | EP1 plan · 3 Function apps + identities · the Static Web App (+ linked backend on Standard) |
