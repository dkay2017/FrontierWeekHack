using TireForge.Core.Agents;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Foundry;

/// <summary>
/// A1 backed by the hosted <c>anomaly-detection-agent</c> (Stage M, Challenge 1).
/// The agent calls <c>check_thresholds</c> and writes the grounded narrative;
/// <c>IsAnomaly</c> stays deterministic on T1 (Decision D12).
/// </summary>
public sealed class FoundryAnomalyDetector(FoundryAgentClient client, FoundryAgentOptions options) : IAnomalyDetector
{
    public async Task<AnomalyVerdict> DetectAsync(
        Reading reading, ThresholdReport t1, IReadOnlyList<Reading> recent, CancellationToken ct = default)
    {
        if (t1.ReadingId != reading.Id)
            throw new ArgumentException($"T1 report is for '{t1.ReadingId}', not '{reading.Id}'.");

        var isAnomaly = t1.AnyBreach; // deterministic — T1 owns "is there an anomaly"

        var prompt =
            $"Check machine {reading.MachineId} (reading {reading.Id}). " +
            $"Call check_thresholds for machine_id \"{reading.MachineId}\", then report its status " +
            "and every sensor reading that is out of spec.";

        var toolResult = ThresholdsTool.Serialize(t1);
        var inv = await client.InvokeAsync(
            options.AnomalyAgentName, prompt,
            toolHandler: (name, _) => name == ThresholdsTool.Name
                ? toolResult
                : $$"""{"error":"unknown tool '{{name}}'"}""",
            ct);

        var text = $"A1 {reading.Id} {reading.MachineId}: {inv.Text.Trim()}";
        return new AnomalyVerdict(isAnomaly, text, new[] { reading.Id });
    }
}
