using System.Diagnostics;
using System.Text;
using TireForge.Core.Agents;
using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Foundry;

/// <summary>
/// A2 backed by the hosted <c>fault-diagnosis-agent</c> (Stage M, Challenge 1).
/// The agent names the cause and writes the LIKELY CAUSE / ACTIONS / URGENCY prose;
/// severity, confidence and citations stay deterministic (Decision D12).
/// </summary>
public sealed class FoundryFaultDiagnoser(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IFaultDiagnoser
{
    public async Task<FaultVerdict> DiagnoseAsync(
        Reading reading, ThresholdReport t1, HistoryReport t2, AnomalyVerdict a1, CancellationToken ct = default)
    {
        if (!a1.IsAnomaly)
            throw new InvalidOperationException("Fault Diagnosis runs only on anomalous readings.");

        var severity = FaultHeuristics.Escalate(t1.Severity, t2);
        var confidence = FaultHeuristics.Score(t1, t2);
        var cites = new List<string> { reading.Id };
        cites.AddRange(t2.Cites);

        var inv = await client.InvokeAsync(options.FaultAgentName, BuildPrompt(reading, t1, t2), toolHandler: null, ct);

        await recorder.RecordAsync(new AgentCallUsage(
            options.FaultAgentName, options.Model, inv.InputTokens, inv.OutputTokens,
            inv.ToolCalls, reading.Id, Activity.Current?.TraceId.ToString()), ct);

        var fault = ExtractLikelyCause(inv.Text)
                    ?? (t1.Breaches.Count > 0
                        ? $"{t1.Breaches[0].Sensor.Slug()} anomaly — see diagnosis"
                        : "fault indicated — see diagnosis");

        var text = $"A2 {reading.Id} {reading.MachineId}: {inv.Text.Trim()}";

        return new FaultVerdict
        {
            Fault = fault,
            Severity = severity,
            Confidence = confidence,
            Text = text,
            Cites = cites,
        }.Validate();
    }

    private static string BuildPrompt(Reading reading, ThresholdReport t1, HistoryReport t2)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Machine {reading.MachineId}, reading {reading.Id}. Sensor anomalies:");
        foreach (var b in t1.Breaches)
        {
            var dir = b.Status == SensorStatus.High ? "above max" : "below min";
            sb.AppendLine(
                $"- {b.Sensor.Slug()}: {b.Value} {b.Unit} ({b.DeviationPct:0.0}% {dir}; band {b.Min}–{b.Max})");
        }

        sb.AppendLine(t2.AnyMatch
            ? $"\nRelevant maintenance history: {string.Join("; ", t2.Incidents.Select(i => $"{i.Id} — {i.Fault} ({i.Severity})"))}"
            : "\nNo comparable prior incidents on record.");

        sb.AppendLine("\nDiagnose the root cause, recommend maintenance actions, and give an urgency.");
        return sb.ToString();
    }

    /// <summary>Pull a short fault label from the agent's "LIKELY CAUSE:" line — first sentence, ≤ 120 chars.</summary>
    private static string? ExtractLikelyCause(string text)
    {
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim().TrimStart('*', '-', '#', ' ');
            var idx = line.IndexOf("LIKELY CAUSE", StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;

            var after = line[(idx + "LIKELY CAUSE".Length)..].Trim(':', '*', ' ', '.');
            if (after.Length == 0) continue;

            var dot = after.IndexOf('.');
            if (dot > 10) after = after[..dot];

            if (after.Length > 120)
            {
                var cut = after.LastIndexOf(' ', 119);
                after = (cut > 40 ? after[..cut] : after[..120]).TrimEnd(',') + "…";
            }
            return after;
        }
        return null;
    }
}
