using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Zynara.Agents;
using Zynara.ApiProxy;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Diagnostics;
using Zynara.Core.View;
using Zynara.Data;

// Experience layer read API (evaluator P1-1 / P1-2). It runs a request through
// Zynara.Core's pipeline, projects the result to a reviewer CaseView, and serves
// it to the dashboard. Self-contained for the demo: the in-memory case store and
// the DemoWorld seed stand in until Zynara.Data (Cosmos) and the orchestrator
// write path are wired.

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddZynaraAgents(context.Configuration);
        services.AddZynaraCore();

        // Challenge 2 — agent-keyed traces (see the orchestrator Program.cs).
        var appInsights = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsights))
            services.AddOpenTelemetry().WithTracing(t => t
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("zynara-apiproxy"))
                .AddSource(ZynaraTelemetry.SourceName)
                .AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsights));

        var cosmos = context.Configuration["COSMOS_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(cosmos))
            services.AddZynaraData(cosmos, context.Configuration["COSMOS_DATABASE"] ?? CosmosNames.Database);
        else
            services.AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()));
        // ICaseRepository: Cosmos when wired, else InMemory (AddZynaraCore default).
    })
    .Build();

var cosmosWired = !string.IsNullOrWhiteSpace(host.Services.GetRequiredService<IConfiguration>()["COSMOS_ENDPOINT"]);

await host.Services.EnsureZynaraAgentsAsync();

// Offline demo only: pre-run the scenarios so the review queue has content on
// first load. Skipped when Cosmos is wired (the orchestrator writes real cases).
if (Environment.GetEnvironmentVariable("ZYNARA_SKIP_DEMO_SEED") != "true" && !cosmosWired)
{
    using var scope = host.Services.CreateScope();
    var cases = scope.ServiceProvider.GetRequiredService<CaseService>();
    foreach (var scenario in DemoCatalog.All)
        await cases.RunAsync(scenario.Request);
}

host.Run();
