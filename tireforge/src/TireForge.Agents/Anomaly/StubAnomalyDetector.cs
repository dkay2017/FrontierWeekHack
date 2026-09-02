using TireForge.Core.Agents;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents;

/// <summary>
/// Deterministic A1 stub (Build Plan Stage D step 2): a reading is anomalous iff
/// any sensor is out of band per the T1 report. Real Foundry agent swaps in at
/// Stage M; the pipeline is identical either way.
/// </summary>
public sealed class StubAnomalyDetector : IAnomalyDetector
{
    public Task<AnomalyVerdict> DetectAsync(
        Reading reading, ThresholdReport t1, IReadOnlyList<Reading> recent, CancellationToken ct = default)
    {
        if (t1.ReadingId != reading.Id)
            throw new ArgumentException($"T1 report is for '{t1.ReadingId}', not '{reading.Id}'.");

        var isAnomaly = t1.AnyBreach;

        string text;
        if (isAnomaly)
        {
            var names = string.Join(", ", t1.Breaches.Select(b => b.Sensor.Slug()));
            text = $"A1 {reading.Id} {reading.MachineId}: {t1.Breaches.Count} sensor(s) out of band ({names}); " +
                   $"T1 severity {t1.Severity} — anomaly";
        }
        else
        {
            text = $"A1 {reading.Id} {reading.MachineId}: all sensors within spec — no anomaly";
        }

        return Task.FromResult(new AnomalyVerdict(isAnomaly, text, new[] { reading.Id }));
    }
}
