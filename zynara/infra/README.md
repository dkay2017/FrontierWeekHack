# infra/ — Care Approval IQ

Bicep for the Azure footprint (TDD §12). **Skeleton — not yet wired to a live
subscription.** `az bicep build infra/main.bicep` is clean; `azd up` /
`az deployment sub create` would provision it.

## The point of this template

It encodes the identity + network boundary the evaluator review (§13) asked to
see in code:

| Component | Identity | Gets | Deliberately does NOT get |
|---|---|---|---|
| orchestrator, api-proxy (**reasoning plane**) | `id-zynara-reasoning` | Foundry inference · Cosmos data-plane · Blob read · App Insights | any route to the payer network; the payer credential |
| **Submission Adapter** | `id-zynara-submission` | Cosmos write (the outcome) · the payer-integration secret · `snet-submission` (the one subnet that can reach the payer) | Foundry / any model access |
| dashboard | `id-zynara-dashboard` | — (static content) | everything |

`snet-compute`'s NSG **denies egress** to `snet-submission` and to
`payerAddressPrefix`. Cosmos and Blob are reachable only over private endpoints
in `snet-data`. So a prompt-injected reasoning agent still cannot open a socket
to a payer — the packets are dropped at the subnet edge.

## Modules

| File | What |
|---|---|
| `modules/identity.bicep` | the three user-assigned managed identities |
| `modules/network.bicep` | VNet · `snet-compute` / `snet-submission` / `snet-data` · the compute NSG |
| `modules/foundry.bicep` | AI Services account + project + model + Log Analytics + App Insights; Cognitive Services User for the reasoning identity only |
| `modules/data.bicep` | Cosmos serverless (containers: requests · submissions · outcomes · caseAudit · agentCalls · denialCohorts) + Blob corpus, both private-endpoint only |
| `modules/keyvault.bicep` | the payer-integration secret, `Key Vault Secrets User` for `id-submission` only |
| `modules/apps.bicep` | EP1 plan · 3 Function apps on the right subnet + identity · the Static Web App (+ linked backend on Standard) |

## Still to do for a real deployment

- Private DNS zones for the private endpoints
- The `Zynara.Submission` project (the adapter itself)
- Payer connectivity (APIM / private link to the clearing house) behind `snet-submission`
- WAF / Front Door, DR (multi-region Cosmos), Key Vault purge protection
- Container-level scoping of the Cosmos data-plane role assignments
