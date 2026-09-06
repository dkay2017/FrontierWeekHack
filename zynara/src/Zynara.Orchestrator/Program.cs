using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Orchestrator;

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

        // Slice 4 has no Cosmos yet — an in-memory store seeded with the demo world.
        // Zynara.Data will register the Cosmos-backed IZynaraStore in its place.
        services.AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()));
    })
    .Build();

// No-op with stubs; provisions the hosted agents when ZYNARA_AGENTS=foundry.
await host.Services.EnsureZynaraAgentsAsync();

host.Run();
