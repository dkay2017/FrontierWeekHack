using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Recovery;
using Zynara.Core.View;

// Regenerates the dashboard's offline dataset: runs every DemoCatalog scenario
// through the pipeline (stub agents) and writes the projected CaseViews.
//
//   dotnet run --project tools/Zynara.DemoDump -- src/Zynara.Dashboard/demo-cases.json

var outPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "demo-cases.json");

var sp = new ServiceCollection()
    .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
    .AddZynaraAgents()
    .AddZynaraCore()
    .BuildServiceProvider();

var cases = sp.GetRequiredService<CaseService>();

var views = new List<CaseView>();
foreach (var scenario in DemoCatalog.All)
    views.Add((await cases.RunAsync(scenario.Request)).View);

var recovery = await sp.GetRequiredService<RecoveryService>().GetAsync();

var payload = new
{
    scenarios = DemoCatalog.All.Select(s => new { s.Id, s.Label, s.Expectation }),
    cases = views,
    recovery,
};

var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() },
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
});

File.WriteAllText(outPath, json);
Console.WriteLine($"wrote {views.Count} cases → {outPath}");
