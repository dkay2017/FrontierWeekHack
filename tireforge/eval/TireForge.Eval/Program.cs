// TireForge.Eval — the CI-gate evaluation harness (Challenge 3, superset).
//
//   dotnet run --project tireforge/eval/TireForge.Eval [-- --min-accuracy 1.0] [--json report.json]
//
// Challenge 3's portal run scores Coherence + Fluency (writing quality) with an
// LLM judge. This harness scores what that can't: **correctness**. It replays the
// 10-case dataset through the deterministic core of the anomaly path
// (ThresholdCheck / T1 — which is what the agent's classification rests on,
// Decision D12) and gates the build on the classification + urgency match rate.
// Deterministic, offline, fast — exactly what a CI quality gate needs.

using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

var args0 = args.ToList();
double minAccuracy = ArgValue("--min-accuracy") is { } m ? double.Parse(m, CultureInfo.InvariantCulture) : 1.0;
string? jsonOut = ArgValue("--json");
string datasetPath = ArgValue("--dataset")
    ?? Path.Combine(FindRepoRoot(), "factory", "challenge-4-deploy", "evaluation_dataset.json");

Console.WriteLine($"TireForge.Eval — dataset: {datasetPath}");
Console.WriteLine($"gate: classification accuracy >= {minAccuracy:P0}\n");

var cases = JsonSerializer.Deserialize<List<EvalCase>>(
    File.ReadAllText(datasetPath),
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    })
    ?? throw new InvalidOperationException("Empty dataset.");

var results = new List<CaseResult>();
foreach (var c in cases)
{
    var (machine, reading) = Parse(c.Input);
    var t1 = ThresholdCheck.Evaluate(reading, machine);

    var predClass = Classification(t1.Severity);
    var predUrgency = Urgency(t1.Severity);

    results.Add(new CaseResult(
        c.Id, machine.Id,
        c.ExpectedOutput.Classification, predClass,
        c.ExpectedOutput.Urgency, predUrgency,
        c.ExpectedOutput.Anomalies.Count, t1.Breaches.Count));
}

// --- report -------------------------------------------------------------
Console.WriteLine($"{"case",-10} {"machine",-9} {"expected",-10} {"predicted",-10} {"urgency",-16} {"anoms",-9} ok");
Console.WriteLine(new string('-', 78));
foreach (var r in results)
{
    var ok = r.ClassOk && r.UrgencyOk;
    Console.WriteLine(
        $"{r.Id,-10} {r.MachineId,-9} {r.Expected,-10} {r.Predicted,-10} " +
        $"{$"{r.ExpectedUrgency}->{r.PredictedUrgency}",-16} {$"{r.PredictedAnomalies}/{r.ExpectedAnomalies}",-9} " +
        (ok ? "PASS" : "FAIL"));
}

var classAcc = results.Count(r => r.ClassOk) / (double)results.Count;
var urgencyAcc = results.Count(r => r.UrgencyOk) / (double)results.Count;

Console.WriteLine(new string('-', 78));
Console.WriteLine($"classification accuracy : {classAcc:P1}  ({results.Count(r => r.ClassOk)}/{results.Count})");
Console.WriteLine($"urgency accuracy        : {urgencyAcc:P1}  ({results.Count(r => r.UrgencyOk)}/{results.Count})");

if (jsonOut is not null)
{
    File.WriteAllText(jsonOut, JsonSerializer.Serialize(new
    {
        classificationAccuracy = classAcc,
        urgencyAccuracy = urgencyAcc,
        minAccuracy,
        cases = results,
    }, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"\nwrote {jsonOut}");
}

var pass = classAcc >= minAccuracy;
Console.WriteLine(pass
    ? $"\nGATE PASS — classification accuracy {classAcc:P1} >= {minAccuracy:P0}"
    : $"\nGATE FAIL — classification accuracy {classAcc:P1} < {minAccuracy:P0}");
return pass ? 0 : 1;

// --- helpers ----------------------------------------------------------
string? ArgValue(string name)
{
    var i = args0.IndexOf(name);
    return i >= 0 && i + 1 < args0.Count ? args0[i + 1] : null;
}

static string Classification(Severity s) => s switch
{
    Severity.Info => "normal",
    Severity.Warn => "warning",
    Severity.Crit => "critical",
    _ => "unknown",
};

static string Urgency(Severity s) => s switch
{
    Severity.Info => "low",
    Severity.Warn => "medium",
    Severity.Crit => "high",
    _ => "unknown",
};

static (Machine, Reading) Parse(string input)
{
    string id = Match(input, @"\(([A-Z]{2}-\d{3})\)");
    string name = Match(input, @"Machine:\s*([a-z_]+)");

    double R(string sensor) => double.Parse(
        Match(input, sensor + @"=([\d.]+)"), CultureInfo.InvariantCulture);

    SensorBand Band(string key, string unit)
    {
        var m = Regex.Match(input, key + @"\s+([\d.]+)-([\d.]+)");
        return new SensorBand
        {
            Min = double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
            Max = double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
            Unit = unit,
        };
    }

    var machine = new Machine
    {
        Id = id,
        Name = name,
        Temperature = Band("temp", "celsius"),
        Pressure = Band("pressure", "bar"),
        Vibration = Band("vibration", "mm/s"),
        Rpm = Band("rpm", "rpm"),
    };

    var reading = new Reading
    {
        Id = Ids.Reading(DateTimeOffset.UnixEpoch),
        MachineId = id,
        CapturedAt = DateTimeOffset.UnixEpoch,
        Temperature = R("temperature"),
        Pressure = R("pressure"),
        Vibration = R("vibration"),
        Rpm = R("rpm"),
    };
    return (machine, reading);
}

static string Match(string s, string pattern)
{
    var m = Regex.Match(s, pattern);
    if (!m.Success) throw new FormatException($"'{pattern}' not found in: {s}");
    return m.Groups[1].Value;
}

static string FindRepoRoot()
{
    for (var d = new DirectoryInfo(AppContext.BaseDirectory); d is not null; d = d.Parent)
        if (File.Exists(Path.Combine(d.FullName, "factory", "challenge-4-deploy", "evaluation_dataset.json")))
            return d.FullName;
    throw new InvalidOperationException("Could not locate the evaluation dataset above the working directory.");
}

// --- dataset shape --------------------------------------------------
sealed record EvalCase(string Id, string Input, ExpectedOutput ExpectedOutput);
sealed record ExpectedOutput(string Classification, List<string> Anomalies, string Urgency, string RecommendedAction);
sealed record CaseResult(
    string Id, string MachineId,
    string Expected, string Predicted,
    string ExpectedUrgency, string PredictedUrgency,
    int ExpectedAnomalies, int PredictedAnomalies)
{
    public bool ClassOk => string.Equals(Expected, Predicted, StringComparison.OrdinalIgnoreCase);
    public bool UrgencyOk => string.Equals(ExpectedUrgency, PredictedUrgency, StringComparison.OrdinalIgnoreCase);
}
