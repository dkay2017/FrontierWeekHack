using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using TireForge.Agents;
using TireForge.Core.Observability;
using TireForge.Core.Pipeline;
using TireForge.Data;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// The pipeline runs inside one durable activity (Decision D2): EF stores + the
// agent ports (stub or real Foundry, per TIREFORGE_AGENTS) + Core.Pipeline itself.
var connectionString = Environment.GetEnvironmentVariable("TIREFORGE_DB")
                       ?? "Data Source=tireforge.db";
builder.Services.AddTireForgeData(connectionString);
builder.Services.AddTireForgeAgents();
builder.Services.AddScoped<Pipeline>();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .WithTracing(tracing => tracing.AddSource(Telemetry.SourceName))
        .UseAzureMonitorExporter();
}

var app = builder.Build();

// Local / demo: migrate + seed on startup. Harmless if already applied.
if (Environment.GetEnvironmentVariable("TIREFORGE_SKIP_DB_INIT") != "true")
{
    await app.Services.InitializeTireForgeDataAsync();
}

app.Run();
