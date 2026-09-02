# infra

Bicep + `azd` infrastructure for TireForge (cross-cutting).

Planned resources:

- Storage account (queues for the ingestion → orchestrator hand-off, Durable Task hub)
- Function App(s) — Flex Consumption, .NET 8 isolated
- Azure AI Foundry project + model deployment (for `TireForge.Agents` real `IAgentClient`)
- Static Web App (dashboard)
- Application Insights + Log Analytics
- Managed identity + RBAC role assignments

## Files

| File | Purpose |
|---|---|
| `main.bicep` | subscription-scoped entry point |
| `main.parameters.json` | azd parameter bindings |

## Deploy

```bash
azd up
```
