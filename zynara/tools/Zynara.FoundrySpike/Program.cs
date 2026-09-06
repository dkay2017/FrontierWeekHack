using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Core;
using Zynara.Core.Abstractions;
using Zynara.Core.Demo;
using Zynara.Core.View;

// De-risk spike (STATUS "next" #1). Runs the WHOLE pipeline against the HOSTED
// Foundry agents — needs-auth, evidence-gap, claims-extraction, appeal-builder,
// critic — for two demo scenarios, and prints what each produced.
//
//   az login
//   export PROJECT_ENDPOINT="https://<acct>.services.ai.azure.com/api/projects/<project>"
//   export MODEL_DEPLOYMENT_NAME="gpt-5.4"          # optional, defaults to gpt-5.4
//   export VECTOR_STORE_ID="<id>"                   # optional, enables File Search
//   dotnet run --project tools/Zynara.FoundrySpike
//
// Prints PASS/FAIL per scenario. Exit 0 = every hosted agent returned parseable
// output and the pipeline routed the case.

Environment.SetEnvironmentVariable("ZYNARA_AGENTS", "foundry");

var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

if (string.IsNullOrWhiteSpace(config["PROJECT_ENDPOINT"]))
{
    Console.Error.WriteLine("PROJECT_ENDPOINT is not set. See the header of this file.");
    return 2;
}

var sp = new ServiceCollection()
    .AddSingleton<IZynaraStore>(_ => DemoWorld.Seed(new InMemoryZynaraStore()))
    .AddZynaraAgents(config)
    .AddZynaraCore()
    .BuildServiceProvider();

Console.WriteLine($"Foundry project : {config["PROJECT_ENDPOINT"]}");
Console.WriteLine($"Model           : {config["MODEL_DEPLOYMENT_NAME"] ?? "gpt-5.4"}");
Console.WriteLine($"File Search      : {(string.IsNullOrWhiteSpace(config["VECTOR_STORE_ID"]) ? "off" : "on")}");
Console.WriteLine(new string('─', 60));

try
{
    Console.WriteLine("Provisioning agents (create-or-ensure)…");
    await sp.EnsureZynaraAgentsAsync();
    Console.WriteLine("  ok\n");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Provisioning failed: {ex.Message}");
    Console.Error.WriteLine("Check: az login, the endpoint, and that the model deployment exists.");
    return 3;
}

var cases = sp.GetRequiredService<CaseService>();
var scenarios = new[] { "demo-ready", "demo-appeal-critic" };
var allOk = true;

foreach (var id in scenarios)
{
    var scenario = DemoCatalog.All.Single(s => s.Id == id);
    Console.WriteLine($"▶ {scenario.Label}");
    var sw = System.Diagnostics.Stopwatch.StartNew();

    try
    {
        var record = await cases.RunAsync(scenario.Request);
        sw.Stop();
        var v = record.View;

        Console.WriteLine($"  status     : {v.Status}   (expected route ≈ {scenario.Expectation.Split('→').Last().Trim().TrimEnd('.')})");
        Console.WriteLine($"  gate       : {v.Gate?.Route} — {v.Gate?.Reason.Split(". [")[0]}");
        Console.WriteLine("  evidence   :");
        foreach (var c in v.Criteria)
            Console.WriteLine($"     [{c.Id}] {c.Status,-12} {(c.EvidenceStatement is { Length: > 0 } e ? $"“{Trim(e, 70)}”" : "—")}");
        if (v.Critic is { } critic)
        {
            Console.WriteLine($"  critic     : {critic.Verdict} — {Trim(critic.Summary, 90)}");
            foreach (var f in critic.Flags) Console.WriteLine($"     ⚑ {f.Check}: {Trim(f.Concern, 80)}");
        }
        Console.WriteLine($"  conflicts  : {(v.Conflicts.Count == 0 ? "none" : string.Join(" | ", v.Conflicts))}");
        Console.WriteLine($"  assembled  : {sw.ElapsedMilliseconds} ms\n");
    }
    catch (Exception ex)
    {
        allOk = false;
        Console.Error.WriteLine($"  FAIL: {ex.GetType().Name} — {ex.Message}\n");
    }
}

Console.WriteLine(new string('─', 60));
Console.WriteLine(allOk ? "PASS — the hosted pipeline ran end to end." : "FAIL — see errors above.");
return allOk ? 0 : 1;

static string Trim(string s, int n) => s.Length <= n ? s : s[..n].TrimEnd() + "…";
