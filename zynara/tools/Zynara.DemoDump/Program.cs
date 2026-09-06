using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.Impact;
using Zynara.Core.Profiles;
using Zynara.Core.Recovery;
using Zynara.Core.View;

// Regenerates the dashboard's offline dataset: runs every DemoCatalog scenario
// through the pipeline (stub agents) and writes the projected CaseViews, the
// recoverable-value report and the regulatory profiles.
//
//   dotnet run --project tools/Zynara.DemoDump -- src/Zynara.Dashboard/demo-cases.json
//
// Also writes config/profiles/<id>.json alongside, so the built-in profiles and
// the deployment-override files never drift.

var outPath = args.Length > 0
    ? args[0]
    : Path.Combine(AppContext.BaseDirectory, "demo-cases.json");

var jsonOpts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
{
    Converters = { new JsonStringEnumConverter() },
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = true,
};

// Fixed clock so the audit timestamps in the committed snapshot are deterministic.
var sp = new ServiceCollection()
    .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
    .AddSingleton<TimeProvider>(new FixedClock(new DateTimeOffset(2026, 9, 6, 0, 0, 0, TimeSpan.Zero)))
    .AddZynaraAgents()
    .AddZynaraCore()
    .BuildServiceProvider();

var cases = sp.GetRequiredService<CaseService>();

var views = new List<CaseView>();
foreach (var scenario in DemoCatalog.All)
    views.Add((await cases.RunAsync(scenario.Request)).View);

var recovery = await sp.GetRequiredService<RecoveryService>().GetAsync();

// AssembledInMs is a live-only timing — zero it so the committed snapshot is deterministic (CI checks it).
var bench = await sp.GetRequiredService<BenchmarkService>().GetAsync();
var benchmark = bench with { Measured = bench.Measured with { AvgAssembledInMs = 0 } };

var payload = new
{
    scenarios = DemoCatalog.All.Select(s => new { s.Id, s.Label, s.Expectation }),
    cases = views,
    recovery,
    benchmark,
    profiles = RegulatoryProfiles.All,
};

File.WriteAllText(outPath, JsonSerializer.Serialize(payload, jsonOpts));
Console.WriteLine($"wrote {views.Count} cases → {outPath}");

// config/profiles/<id>.json — the override format, generated so it can't drift.
var profilesDir = Path.Combine(
    Path.GetDirectoryName(Path.GetFullPath(outPath))!, "..", "..", "config", "profiles");
try
{
    Directory.CreateDirectory(profilesDir);
    foreach (var p in RegulatoryProfiles.All)
    {
        var path = Path.Combine(profilesDir, p.Id + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(p, jsonOpts));
        Console.WriteLine($"wrote profile → {Path.GetFullPath(path)}");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"(skipped config/profiles: {ex.Message})");
}

/// <summary>A <see cref="TimeProvider"/> frozen at one instant — deterministic snapshots.</summary>
file sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
