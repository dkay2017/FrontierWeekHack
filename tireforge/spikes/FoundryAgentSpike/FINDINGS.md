# Stage M spike — findings (2026-09-03)

**Result: rung 0 (pure C#) WORKS.** No Python, no portal clicks. The D11 fallback
ladder stays documented but we don't need it.

## What the spike did

`Program.cs`, run with `dotnet run` + `az login`:

1. Created a persistent Foundry agent `anomaly-detection-agent` (version `1`) from
   C#, using the **nextgen Foundry projects (2.x) API** — the same surface as
   `factory/challenge-1-build/agents.py`, not the older `Azure.AI.Agents.Persistent`.
2. Invoked it once with a pasted sensor payload (no tool call).
3. Agent output was correct: **2 warnings (MX-001, IS-005) + 1 critical (CP-003)** —
   Challenge 1's success criterion, reproduced by a real hosted agent.
4. The agent is portal-visible (confirmed via REST `GET /agents` — `kind: prompt`,
   `model: gpt-5.4`, `status: active`, has an `agent_guid` + managed identity).
5. Trace reached App Insights: dependency span **`invoke_agent anomaly-detection-agent:1`**,
   `type: AI` — i.e. an agent-keyed GenAI span. That's Challenge 2's requirement.

## The working C# API (packages + calls)

```
dotnet add package Azure.AI.Projects          --version 2.0.1
dotnet add package Azure.AI.Projects.Agents    --version 2.0.0
dotnet add package Azure.AI.Extensions.OpenAI  --version 2.0.0
dotnet add package Azure.Identity              --version 1.21.0
```

- `Azure.Core` 1.53 makes `TokenCredential` an `System.ClientModel.AuthenticationTokenProvider`,
  so `new DefaultAzureCredential()` passes straight into the new clients — no adapter.

**Create the agent:**

```csharp
var admin = new AgentAdministrationClient(endpoint, cred);   // endpoint = PROJECT_CONNECTION_STRING
var def   = new DeclarativeAgentDefinition(model) { Instructions = systemPrompt };
ProjectsAgentVersion agent = admin.CreateAgentVersion(
    "anomaly-detection-agent", new ProjectsAgentVersionCreationOptions(def));
// agent.Id = "anomaly-detection-agent:1", agent.Version = "1"
```

`DeclarativeAgentDefinition.Tools` (an `IList<ResponseTool>`) is where the
`check_thresholds` function tool goes for Stage M full — `ResponseTool.CreateFunctionTool(...)`.
`CreateAgentVersion` called again bumps the version; it does not duplicate the agent.

**Invoke it:**

```csharp
using OpenAI.Responses;                       // ResponseResult
#pragma warning disable OPENAI001              // Responses API is still preview

var agentRef  = new AgentReference(agent.Name, version: null);   // Type is a fixed "agent_reference"
var responses = new ProjectResponsesClient(endpoint, cred, agentRef,
                                           defaultConversationId: null, options: null);
ResponseResult r = responses.CreateResponse(userText).Value;
string text = r.GetOutputText();
var usage   = r.Usage;   // InputTokenCount / OutputTokenCount / TotalTokenCount
```

For a multi-turn / tool loop, create a conversation first and pass its id as
`defaultConversationId`; iterate on `r.OutputItems` for `function_call` items
(mirrors the `while True` loop in `agents.py`).

## Tracing wiring that worked

- `AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true)` +
  `OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT=true`.
- `Sdk.CreateTracerProviderBuilder().AddSource("Azure.*").AddSource("OpenAI.*")
  .AddAzureMonitorTraceExporter(...)`.
- The `invoke_agent` span exported; our own `ActivitySource("TireForge.Spike")` span
  did not appear as a dependency in the quick check — not chased, the pipeline's
  own tracing stage (J.5) already covers our spans and the Functions hosts wire the
  exporter the same way.

## Implications for Stage M full

- Behind `IAnomalyDetector` / `IFaultDiagnoser` / `IWorkOrderDrafter`, the real impls
  in `TireForge.Agents` construct an `AgentAdministrationClient` + `ProjectResponsesClient`
  exactly as above.
- Agent provisioning (`CreateAgentVersion` for all three, idempotent-ish by version)
  can live in a startup path or a tiny `dotnet run` provisioner — still pure C#.
- `check_thresholds` becomes a `ResponseTool` function tool on the anomaly agent;
  the tool body calls our existing `ThresholdCheck` (Core), so the agent's tool and
  our deterministic T1 share one implementation.
- Challenges 3 & 4 are now unblocked (the agent exists as a portal resource).
