using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Zynara.Core.Abstractions;
using Zynara.Core.Diagnostics;
using Zynara.Core.Submission;
using Zynara.Data;
using Zynara.Submission;

// The Submission Adapter (Architecture §4 · TDD §7). The only component that
// touches a payer. Runs under id-submission — Key Vault Secrets User on the vault,
// Cosmos write, and deliberately NO Foundry (no PROJECT_ENDPOINT).
//
//   POST /api/submit/{requestId}  → send a reviewer-approved case, once.

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddScoped<SubmissionService>();

        var cosmos = context.Configuration["COSMOS_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(cosmos))
        {
            services.AddZynaraData(cosmos, context.Configuration["COSMOS_DATABASE"] ?? CosmosNames.Database);
        }
        else
        {
            services.TryAddSingleton<ICaseRepository, InMemoryCaseRepository>();
            services.TryAddSingleton<ISubmissionStore, InMemorySubmissionStore>();
        }

        // Key Vault present → the production-shaped gateway (reads the payer
        // credential); otherwise the deterministic stub for local runs and the demo.
        var keyVault = context.Configuration["KEY_VAULT_URI"];
        if (!string.IsNullOrWhiteSpace(keyVault))
            services.AddSingleton<IPayerGateway>(sp =>
                new KeyVaultPayerGateway(keyVault, sp.GetRequiredService<ILogger<KeyVaultPayerGateway>>()));
        else
            services.TryAddSingleton<IPayerGateway, StubPayerGateway>();

        var appInsights = context.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        if (!string.IsNullOrWhiteSpace(appInsights))
            services.AddOpenTelemetry().WithTracing(t => t
                .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("zynara-submission"))
                .AddSource(ZynaraTelemetry.SourceName)
                .AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsights));
    })
    .Build();

host.Run();
