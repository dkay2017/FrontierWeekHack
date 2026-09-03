// TireForge — Stage M spike (D9, rung 0).
// Goal: prove pure C# can (1) create a persistent Foundry agent that shows up in
// Build -> Agents and (2) invoke it once, with the trace reaching App Insights.
//
//   dotnet run --project tireforge/spikes/FoundryAgentSpike
//
// Reads factory/.env (PROJECT_CONNECTION_STRING, MODEL_DEPLOYMENT_NAME,
// APPLICATIONINSIGHTS_CONNECTION_STRING). Needs `az login`.

using System.Diagnostics;
using Azure.AI.Extensions.OpenAI;
using Azure.AI.Projects.Agents;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenAI.Responses;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

#pragma warning disable OPENAI001 // OpenAI Responses + experimental telemetry are preview

const string AgentName = "anomaly-detection-agent";

// agents.py — AnomalyDetectionAgent system prompt, verbatim.
const string SystemPrompt = """
    You are an industrial sensor anomaly detection expert for TireForge Industries.
    When asked to check machines, use the check_thresholds tool for each machine.
    For each machine, report:
    - Machine name and ID
    - Status (normal / warning / critical)
    - Each sensor reading that is out of spec: current value, threshold violated, deviation
    Use warning and critical markers for anomalies.
    If all readings are in spec, mark the machine as normal.
    Be concise and structured.
    """;

// Challenge-4 pasted-sensor-data style payload — no tool call needed for the spike.
const string UserMessage = """
    All sensor readings for today are below — analyse them directly, do not call check_thresholds.

    MX-001 (mixer) — temperature 92.3C [normal 60-90], pressure 3.1 bar [2.0-4.0], vibration 4.8 mm/s [0-4.5], rpm 58 [40-65]
    EX-002 (extruder) — temperature 115C [100-130], pressure 12.5 bar [10-15], vibration 2.1 mm/s [0-3.5], rpm 30 [20-40]
    CP-003 (curing_press) — temperature 198.5C [140-180], pressure 18.2 bar [12-16], vibration 7.3 mm/s [0-3.0], rpm 0 [0]
    CU-004 (cooling_unit) — temperature 35.2C [20-45], pressure 1.0 bar [0.8-1.5], vibration 0.8 mm/s [0-2.0], rpm 120 [80-150]
    IS-005 (inspection_station) — temperature 28C [18-30], pressure 1.0 bar [0.8-1.2], vibration 5.2 mm/s [0-4.0], rpm 1800 [1500-2200]

    Detect all anomalies. Expected: 2 warnings + 1 critical.
    """;

var env = LoadDotEnv(FindRepoRoot());
string endpointRaw = Require("PROJECT_CONNECTION_STRING");
string model = env.GetValueOrDefault("MODEL_DEPLOYMENT_NAME", "gpt-5.4");
string? appInsights = env.GetValueOrDefault("APPLICATIONINSIGHTS_CONNECTION_STRING");
var endpoint = new Uri(endpointRaw);

// --- tracing: our span + the SDK's gen_ai.* spans -> App Insights ------------
AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);
Environment.SetEnvironmentVariable("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "true");

var ourSource = new ActivitySource("TireForge.Spike");
TracerProvider? tracer = null;
if (!string.IsNullOrWhiteSpace(appInsights))
{
    tracer = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("tireforge-agent-spike"))
        .AddSource("TireForge.Spike")
        .AddSource("Azure.*")
        .AddSource("OpenAI.*")
        .AddSource("Experimental.OpenAI.*")
        .AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsights)
        .Build();
    Console.WriteLine("App Insights exporter wired.");
}
else
{
    Console.WriteLine("No APPLICATIONINSIGHTS_CONNECTION_STRING — running without trace export.");
}

var cred = new DefaultAzureCredential();

using (var run = ourSource.StartActivity("spike.run", ActivityKind.Client))
{
    Console.WriteLine($"\nendpoint : {endpoint}");
    Console.WriteLine($"model    : {model}");
    Console.WriteLine($"trace id : {run?.TraceId}");

    // --- 1. create / update the persistent agent ---------------------------
    Console.WriteLine($"\nCreating agent '{AgentName}' ...");
    var admin = new AgentAdministrationClient(endpoint, cred);
    var definition = new DeclarativeAgentDefinition(model) { Instructions = SystemPrompt };
    ProjectsAgentVersion agent = admin.CreateAgentVersion(
        AgentName, new ProjectsAgentVersionCreationOptions(definition));
    Console.WriteLine($"  created: id={agent.Id} name={agent.Name} version={agent.Version}");

    // --- 2. invoke it once ------------------------------------------------
    Console.WriteLine("\nInvoking the agent ...");
    var agentRef = new AgentReference(agent.Name, version: null);
    var responses = new ProjectResponsesClient(endpoint, cred, agentRef, defaultConversationId: null, options: null);
    ResponseResult response = responses.CreateResponse(UserMessage).Value;

    Console.WriteLine("\n--- agent output ------------------------------------------------");
    Console.WriteLine(response.GetOutputText());
    Console.WriteLine("---------------------------------------------------------------");
    if (response.Usage is { } u)
        Console.WriteLine($"tokens: in={u.InputTokenCount} out={u.OutputTokenCount} total={u.TotalTokenCount}");
}

Console.WriteLine("\nFlushing traces ...");
tracer?.ForceFlush(10_000);
tracer?.Dispose();
Console.WriteLine("Done. Check Build -> Agents in the portal, and App Insights for the trace.");

// --- helpers ---------------------------------------------------------------
string Require(string key) =>
    env.GetValueOrDefault(key) is { Length: > 0 } v
        ? v
        : throw new InvalidOperationException($"{key} missing from factory/.env");

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    for (; dir is not null; dir = dir.Parent)
        if (File.Exists(Path.Combine(dir.FullName, "factory", ".env")))
            return dir.FullName;
    throw new InvalidOperationException("Could not locate factory/.env above the working directory.");
}

static Dictionary<string, string> LoadDotEnv(string repoRoot)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var line in File.ReadAllLines(Path.Combine(repoRoot, "factory", ".env")))
    {
        var t = line.Trim();
        if (t.Length == 0 || t.StartsWith('#')) continue;
        var eq = t.IndexOf('=');
        if (eq <= 0) continue;
        map[t[..eq].Trim()] = t[(eq + 1)..].Trim().Trim('"');
    }
    return map;
}
