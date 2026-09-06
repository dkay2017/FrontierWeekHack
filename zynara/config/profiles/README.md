# config/profiles

Policy + Regulatory Profiles (P1-5 · TDD §7.2 · DECISIONS D14).

`uk.json` / `us.json` are the **deployment-override format**. `Zynara.Core` ships
built-in defaults in `RegulatoryProfiles`; these files are **generated from those
defaults** by `tools/Zynara.DemoDump` so the two never drift — edit
`RegulatoryProfiles.cs`, then regenerate:

```
dotnet run --project tools/Zynara.DemoDump -- src/Zynara.Dashboard/demo-cases.json
```

A profile carries, per region: the policy pack, the terminology map, the coding
conventions, the governing regulators, the appeal / escalation path, the
integration profile, and the currency. It is **not** a product centrepiece
(review guardrail §18) — the dashboard surfaces it as a small header panel.
