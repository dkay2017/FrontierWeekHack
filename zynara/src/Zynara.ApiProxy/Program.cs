using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zynara.Agents;
using Zynara.ApiProxy;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.View;

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
        services.AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()));
        // ICaseRepository defaults to InMemoryCaseRepository (registered by AddZynaraCore).
    })
    .Build();

await host.Services.EnsureZynaraAgentsAsync();

// Pre-run the demo scenarios so the review queue has content on first load.
if (Environment.GetEnvironmentVariable("ZYNARA_SKIP_DEMO_SEED") != "true")
{
    using var scope = host.Services.CreateScope();
    var cases = scope.ServiceProvider.GetRequiredService<CaseService>();
    foreach (var scenario in DemoCatalog.All)
        await cases.RunAsync(scenario.Request);
}

host.Run();
