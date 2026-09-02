# TireForge.Dashboard

Static Web App — the **Experience** layer. Port of dashboard v1.6.

Plain static assets (no `.csproj`, not part of `TireForge.sln`). Deployed as an
Azure Static Web App via `azure.yaml` / `infra/`.

Consumes `TireForge.ApiProxy` endpoints:
`/status`, `/queue`, `/workorders`, `/cost`, `/simulate`, `/decision`.

## Local preview

```bash
npx serve .
```
