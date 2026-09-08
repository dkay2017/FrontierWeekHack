using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Zynara.Agents;
using Zynara.Agents.Foundry;
using Zynara.Eval;

// Captures the credible single-generalist baseline (re-eval item 1). Runs the
// GeneralistBaselineLlm prompt once per labelled case against the hosted Foundry
// model and writes the verbatim response to eval/Zynara.Eval/baseline-fixtures/.
// The eval then replays those fixtures — CI stays offline and deterministic.
//
//   az login
//   export PROJECT_ENDPOINT="https://<acct>.services.ai.azure.com/api/projects/<project>"
//   export MODEL_DEPLOYMENT_NAME="gpt-5.4"        # optional, defaults to gpt-5.4
//   cd zynara && dotnet run --project tools/Zynara.BaselineRefresh
//
// Then: dotnet test eval/Zynara.Eval  → the report now scores the full comparison.
// Commit eval/Zynara.Eval/baseline-fixtures/.

var casesDir = args.Length > 0 ? args[0] : Path.Combine("eval", "Zynara.Eval", "cases");
var fixturesDir = args.Length > 1 ? args[1] : Path.Combine("eval", "Zynara.Eval", "baseline-fixtures");

if (!Directory.Exists(casesDir))
{
    Console.Error.WriteLine($"cases dir not found: '{Path.GetFullPath(casesDir)}' — run from the zynara/ folder.");
    return 2;
}

Environment.SetEnvironmentVariable("ZYNARA_AGENTS", "foundry");
var config = new ConfigurationBuilder().AddEnvironmentVariables().Build();

if (string.IsNullOrWhiteSpace(config["PROJECT_ENDPOINT"]))
{
    Console.Error.WriteLine("PROJECT_ENDPOINT is not set. See the header of this file.");
    return 2;
}

var sp = new ServiceCollection()
    .AddZynaraAgents(config)
    .BuildServiceProvider();

var client = sp.GetRequiredService<FoundryAgentClient>();
var cases = EvalCase.LoadAll(casesDir);

Console.WriteLine($"Foundry project : {config["PROJECT_ENDPOINT"]}");
Console.WriteLine($"Model           : {config["MODEL_DEPLOYMENT_NAME"] ?? "gpt-5.4"}");
Console.WriteLine($"Cases           : {cases.Count} from {Path.GetFullPath(casesDir)}");
Console.WriteLine($"Fixtures →       : {Path.GetFullPath(fixturesDir)}");
Console.WriteLine(new string('-', 60));

try
{
    Console.Write($"Provisioning agent '{GeneralistBaselineLlm.AgentName}' … ");
    await client.CreateVersionAsync(GeneralistBaselineLlm.AgentName, GeneralistBaselineLlm.SystemPrompt);
    Console.WriteLine("ok");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"failed: {ex.Message}");
    Console.Error.WriteLine("Check: az login, the endpoint, and that the model deployment exists.");
    return 3;
}

Directory.CreateDirectory(fixturesDir);
var ok = 0;
var bad = 0;

foreach (var c in cases)
{
    Console.Write($"  {c.Id,-30} ");
    try
    {
        var prompt = GeneralistBaselineLlm.UserPrompt(c);
        var response = await client.InvokeAsync(GeneralistBaselineLlm.AgentName, prompt);
        var parsed = GeneralistBaselineLlm.Parse(response.Text);
        if (parsed is null)
        {
            Console.WriteLine("UNPARSEABLE — response saved anyway; inspect the fixture");
            bad++;
        }
        else
        {
            Console.WriteLine($"route={parsed.Route}");
            ok++;
        }

        var fixture = new GeneralistBaselineLlm.Fixture(GeneralistBaselineLlm.PromptHash(c), response.Text);
        var json = System.Text.Json.JsonSerializer.Serialize(fixture,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(Path.Combine(fixturesDir, c.Id + ".json"), json.ReplaceLineEndings("\n"));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR — {ex.Message}");
        bad++;
    }
}

Console.WriteLine(new string('-', 60));
Console.WriteLine($"{ok} captured, {bad} need attention. Now: dotnet test eval/Zynara.Eval && git add eval/Zynara.Eval/baseline-fixtures");
return bad > 0 ? 1 : 0;
