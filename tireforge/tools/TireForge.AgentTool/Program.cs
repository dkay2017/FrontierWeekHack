// TireForge — Stage M agent tool.
//
//   dotnet run --project tireforge/tools/TireForge.AgentTool -- provision
//   dotnet run --project tireforge/tools/TireForge.AgentTool -- run
//
// provision : push a fresh version of all three Foundry agents (prompts + tool).
// run       : provision, then run one full pipeline pass (CP-003 critical) against
//             the real hosted agents on a seeded in-memory DB, with tracing to
//             App Insights. Proves Challenge 1 (3 agents) + Challenge 2 (spans).
//
// Reads factory/.env. Needs `az login`.

using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using TireForge.Agents;
using TireForge.Agents.Foundry;
using TireForge.Core.Abstractions;
using TireForge.Core.Model;
using TireForge.Core.Observability;
using TireForge.Core.Pipeline;
using TireForge.Data;

var command = args.FirstOrDefault() ?? "run";
var repoRoot = FindRepoRoot();
var env = LoadDotEnv(repoRoot);
foreach (var (k, v) in env) Environment.SetEnvironmentVariable(k, v);
Environment.SetEnvironmentVariable(FoundryAgentOptions.ModeVariable, "foundry");

var appInsights = env.GetValueOrDefault("APPLICATIONINSIGHTS_CONNECTION_STRING");
AppContext.SetSwitch("OpenAI.Experimental.EnableOpenTelemetry", true);
Environment.SetEnvironmentVariable("OTEL_INSTRUMENTATION_GENAI_CAPTURE_MESSAGE_CONTENT", "true");

using var tracer = string.IsNullOrWhiteSpace(appInsights) ? null : Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("tireforge-agent-tool"))
    .AddSource(Telemetry.SourceName)
    .AddSource("Azure.*")
    .AddSource("OpenAI.*")
    .AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsights)
    .Build();

var services = new ServiceCollection();
services.AddTireForgeData($"Data Source={Path.Combine(Path.GetTempPath(), "tireforge-agenttool.db")}");
services.AddTireForgeAgents();
await using var sp = services.BuildServiceProvider();

var provisioner = sp.GetRequiredService<FoundryAgentProvisioner>();

Console.WriteLine("Provisioning agents (new versions) ...");
foreach (var line in await provisioner.CreateVersionsAsync())
    Console.WriteLine($"  {line}");

if (command == "provision")
{
    Console.WriteLine("\nDone. See Build → Agents in the portal.");
    tracer?.ForceFlush(10_000);
    return;
}

// --- run: one full pipeline pass against the real agents --------------------
await sp.InitializeTireForgeDataAsync();

using var scope = sp.CreateScope();
var s = scope.ServiceProvider;
var pipeline = new Pipeline(
    s.GetRequiredService<IMachineStore>(),
    s.GetRequiredService<IReadingStore>(),
    s.GetRequiredService<IHistoryStore>(),
    s.GetRequiredService<IDiagnosisStore>(),
    s.GetRequiredService<IWorkOrderStore>(),
    s.GetRequiredService<TireForge.Core.Agents.IAnomalyDetector>(),
    s.GetRequiredService<TireForge.Core.Agents.IFaultDiagnoser>(),
    s.GetRequiredService<TireForge.Core.Agents.IWorkOrderDrafter>());

// CP-003 curing press — the seeded critical snapshot (Challenge 1 data).
var reading = new Reading
{
    Id = TireForge.Core.Model.Ids.Reading(DateTimeOffset.UtcNow),
    MachineId = "CP-003",
    CapturedAt = DateTimeOffset.UtcNow,
    Temperature = 198.5, Pressure = 18.2, Vibration = 7.3, Rpm = 0,
};

Console.WriteLine($"\nRunning the pipeline for {reading.MachineId} ({reading.Id}) ...\n");
var result = await pipeline.RunAsync(reading);

Console.WriteLine("--- trace ----------------------------------------------------");
foreach (var line in result.Trace) Console.WriteLine(line);
Console.WriteLine("--- diagnosis ------------------------------------------------");
var dx = result.Diagnosis!;
Console.WriteLine($"trace id   : {result.TraceId}");
Console.WriteLine($"fault      : {dx.Fault}");
Console.WriteLine($"severity   : {dx.Severity}   confidence: {dx.Confidence:0.00}");
Console.WriteLine($"gate route : {dx.Route}  ({dx.GateReason})");
Console.WriteLine($"cites      : {dx.IncidentCites}");
Console.WriteLine($"\ndetect (A1):\n{dx.DetectText}");
Console.WriteLine($"\ndiagnose (A2):\n{dx.DiagnoseText}");
Console.WriteLine($"\ndraft work order (A3):\n{dx.DraftActionText}");
Console.WriteLine("-------------------------------------------------------------");

Console.WriteLine("\nFlushing traces ...");
tracer?.ForceFlush(15_000);
Console.WriteLine("Done. Check the pipeline trace + nested agent spans in App Insights.");

// --- helpers --------------------------------------------------------------
static string FindRepoRoot()
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "factory", ".env")))
            return d.FullName;
    throw new InvalidOperationException("factory/.env not found above the working directory.");
}

static Dictionary<string, string> LoadDotEnv(string repoRoot)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    foreach (var raw in File.ReadAllLines(Path.Combine(repoRoot, "factory", ".env")))
    {
        var t = raw.Trim();
        if (t.Length == 0 || t.StartsWith('#')) continue;
        var eq = t.IndexOf('=');
        if (eq > 0) map[t[..eq].Trim()] = t[(eq + 1)..].Trim().Trim('"');
    }
    return map;
}
