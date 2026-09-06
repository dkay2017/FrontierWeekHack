using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Diagnostics;
using Zynara.Data;

// Compute layer (Architecture §4 · TDD §12 TD-1..TD-3).
//
//   POST /api/requests  → the Durable HTTP starter (the async boundary — no queue).
//   AuthOrchestrator    → the deterministic hub: sequences the spokes, owns the Gate.
//   *Activity           → one spoke each (retry isolation, replay-safe resume — TD-2).
//
// The pipeline logic lives in Zynara.Core; this project only schedules it.

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddZynaraAgents(context.Configuration);   // stub by default; foundry via ZYNARA_AGENTS
        services.AddZynaraCore();                          // spokes + Gate + pipeline

        // Challenge 2 — agent-keyed traces. Export the pipeline ActivitySource
        // (pipeline.run → spoke.* → invoke_agent * → chat *) to App Insights.
        // No connection string (local / CI) → the spans are still emitted for the
        // tests' ActivityListener, just not shipped anywhere.
        var appInsights = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsights))
            services.AddOpenTelemetry().WithTracing(t => t
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("zynara-orchestrator"))
                .AddSource(ZynaraTelemetry.SourceName)
                .AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsights));

        // COSMOS_ENDPOINT set → the Cosmos-backed store; otherwise the in-memory demo world.
        var cosmos = context.Configuration["COSMOS_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(cosmos))
            services.AddZynaraData(cosmos, context.Configuration["COSMOS_DATABASE"] ?? CosmosNames.Database);
        else
            services.AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()));
    })
    .Build();

// No-op with stubs; provisions the hosted agents when ZYNARA_AGENTS=foundry.
await host.Services.EnsureZynaraAgentsAsync();

host.Run();
