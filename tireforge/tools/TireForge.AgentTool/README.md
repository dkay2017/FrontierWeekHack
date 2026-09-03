# TireForge.AgentTool

Pure-C# provisioner + smoke runner for the real Foundry agents (Stage M, D11
rung 0). Reads `factory/.env`, needs `az login`.

```bash
# push a fresh version of all three agents (prompts + check_thresholds tool)
dotnet run --project tireforge/tools/TireForge.AgentTool -- provision

# provision, then run one full pipeline pass (CP-003 critical) against the real
# hosted agents on a seeded in-memory DB, tracing to App Insights
dotnet run --project tireforge/tools/TireForge.AgentTool -- run
```

`provision` creates / versions:

| Agent | Tool | Prompt |
|---|---|---|
| `anomaly-detection-agent` | `check_thresholds` (calls `Core.ThresholdCheck`) | `AgentPrompts.AnomalyDetection` (verbatim from `agents.py`) |
| `fault-diagnosis-agent` | — | `AgentPrompts.FaultDiagnosis` (verbatim from `agents.py`) |
| `work-order-agent` | — | `AgentPrompts.WorkOrder` (our superset addition) |

`run` proves **Challenge 1** (3 persistent agents produce the diagnosis) and
**Challenge 2** (agent `invoke_agent` / `chat gpt-5.4` spans nest under the one
`pipeline.run` trace in App Insights).

The agent implementations live in `src/TireForge.Agents/Foundry/`; this tool just
drives them. `TIREFORGE_AGENTS=foundry` is forced on here — everywhere else it
defaults to `stub`.
