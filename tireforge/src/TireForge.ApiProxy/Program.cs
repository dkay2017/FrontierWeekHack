using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;
using TireForge.ApiProxy;
using TireForge.Core.Observability;
using TireForge.Data;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// Read models (Reports) + the reviewer write path (Reviewer) over EF Core / Azure SQL.
// TIREFORGE_DB matches TireForgeDbContextFactory; the fallback is a local SQL Server
// (docker) for dev — Azure SQL uses "Authentication=Active Directory Default".
var connectionString = Environment.GetEnvironmentVariable("TIREFORGE_DB")
                       ?? "Server=localhost,1433;Database=tireforge;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";
builder.Services.AddTireForgeData(connectionString);

// IActionResult payloads serialize through ASP.NET Core's JSON formatter — align it
// with the dashboard contract (camelCase, enums as camelCase strings).
builder.Services.Configure<Microsoft.AspNetCore.Mvc.JsonOptions>(o =>
    ApiJson.Configure(o.JsonSerializerOptions));

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .WithTracing(tracing => tracing.AddSource(Telemetry.SourceName))
        .UseAzureMonitorExporter();
}

var app = builder.Build();

// Local / demo convenience: migrate + seed on startup. Harmless if already applied.
if (Environment.GetEnvironmentVariable("TIREFORGE_SKIP_DB_INIT") != "true")
{
    await app.Services.InitializeTireForgeDataAsync();
}

app.Run();
