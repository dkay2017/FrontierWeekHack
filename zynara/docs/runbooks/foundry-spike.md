# De-risk spike — one hosted pipeline run, end to end

**Goal:** prove the hosted Foundry agents work — not just the deterministic stubs.
`tools/Zynara.FoundrySpike` provisions the five agents, runs the whole pipeline
for two demo scenarios against the real project, and prints what each agent
produced.

## Prerequisites

- A Foundry project (its own resource group — nothing shared with TireForge).
  Provision it with `azd up` (see `infra/`) or by hand:
  ```
  az cognitiveservices account create -n <acct> -g <rg> -l swedencentral --kind AIServices --sku S0
  az cognitiveservices account deployment create -n <acct> -g <rg> \
     --deployment-name gpt-5.4 --model-name gpt-5.4 --model-version 2026-03-05 \
     --model-format OpenAI --sku-name GlobalStandard --sku-capacity 10
  # then create a project in the account (portal or `az` preview)
  ```
- `az login` as a principal with **Cognitive Services User** + **Azure AI User**
  on the project.

## Run

```
az login
export PROJECT_ENDPOINT="https://<acct>.services.ai.azure.com/api/projects/<project>"
export MODEL_DEPLOYMENT_NAME="gpt-5.4"     # optional
export VECTOR_STORE_ID="<id>"              # optional — turns on File Search grounding
dotnet run --project tools/Zynara.FoundrySpike
```

## What a pass looks like

```
▶ Complete record — ready to submit
  status     : ReadyToSubmit
  gate       : ReadyToSubmit — gap-checked, Critic-cleared and ready
  evidence   :
     [c1] Documented   “Physiotherapy completed over eight weeks…”
     ...
  critic     : Clear — No material concern
  assembled  : 41213 ms

▶ Denied — the Critic challenges an over-reaching appeal
  ...
  critic     : Concerns — the recommendation is firmer than Low-quality evidence supports
     ⚑ recommendation-strength: …

PASS — the hosted pipeline ran end to end.
```

Exit 0 = every hosted agent returned parseable output and the Gate routed the
case. Non-zero = an agent failed or the response could not be parsed (the message
says which).

## File Search (recommended before recording the video)

With `VECTOR_STORE_ID` unset the hosted Critic can't see the policy clause text or
the precedent narratives, so it flags *"no policy clause text is quoted"* and the
clean case routes to **Strengthen** instead of ReadyToSubmit. Load the corpus once
(see `data/corpus/README.md`), then:

```
export VECTOR_STORE_ID="vs_..."
dotnet run --project tools/Zynara.FoundrySpike
```

## If it fails

| Symptom | Likely cause |
|---|---|
| `PROJECT_ENDPOINT is not set` | export it |
| `Provisioning failed: … 401 / 403` | `az login`, or the principal lacks the role |
| `Provisioning failed: … model … not found` | the `gpt-5.4` deployment name doesn't exist in the project |
| a scenario `FAIL: … could not be parsed` | the model didn't return the expected JSON — tighten the prompt in `AgentPrompts.cs`; `FoundryResponse` already falls back to "route to a human" |
