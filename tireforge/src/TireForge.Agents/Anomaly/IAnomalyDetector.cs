using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Anomaly;

/// <summary>The Anomaly Detection agent — A1 (Build Plan Stage D).</summary>
public interface IAnomalyDetector
{
    /// <param name="reading">The reading under test.</param>
    /// <param name="t1">Its ThresholdCheck report.</param>
    /// <param name="recent">Recent readings for the same machine, newest first (trend context).</param>
    Task<AnomalyVerdict> DetectAsync(
        Reading reading,
        ThresholdReport t1,
        IReadOnlyList<Reading> recent,
        CancellationToken ct = default);
}

/// <summary>A1 output — <c>{is_anomaly, text, cites}</c>. The text always cites the reading id.</summary>
public sealed record AnomalyVerdict(bool IsAnomaly, string Text, IReadOnlyList<string> Cites)
{
    /// <summary>Apply the verdict to the reading (Build Plan Stage D step 3).</summary>
    public void ApplyTo(Reading reading) => reading.IsAnomaly = IsAnomaly;
}
