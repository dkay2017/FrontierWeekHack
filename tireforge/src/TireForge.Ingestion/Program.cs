using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using TireForge.Core.Observability;
using TireForge.Data;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// The simulator reads the machine roster (+ bands) from the same store the
// pipeline uses, so synthetic readings match the seeded Challenge machines.
var connectionString = Environment.GetEnvironmentVariable("TIREFORGE_DB")
                       ?? "Data Source=tireforge.db";
builder.Services.AddTireForgeData(connectionString);

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .WithTracing(tracing => tracing.AddSource(Telemetry.SourceName))
        .UseAzureMonitorExporter();
}

var app = builder.Build();

if (Environment.GetEnvironmentVariable("TIREFORGE_SKIP_DB_INIT") != "true")
{
    await app.Services.InitializeTireForgeDataAsync();
}

app.Run();
