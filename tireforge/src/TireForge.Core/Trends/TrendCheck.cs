using System.Text;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Trends;

/// <summary>
/// One sensor's projected trajectory: still in spec today, but moving toward its
/// bound at a rate that would cross it within <see cref="TrendCheck.HorizonHours"/>.
/// </summary>
public sealed record TrendWarning(
    SensorKind Sensor,
    double CurrentValue,
    string Unit,
    double RateOfChangePerHour,
    double BoundApproached,
    DateTimeOffset ProjectedBreachAt,
    double HoursToBreachAt,
    double Confidence,
    int SampleCount);

/// <summary>Result of TrendCheck / T0 for one reading — the predictive counterpart to T1.</summary>
public sealed record TrendReport(
    string ReadingId,
    string MachineId,
    IReadOnlyList<TrendWarning> Warnings,
    string Trace)
{
    public bool AnyWarning => Warnings.Count > 0;
}

/// <summary>
/// Predictive early-warning check (T0): fits a simple linear trend per sensor over
/// the recent reading window and flags a sensor that is <b>still in spec</b> but on
/// a trajectory to breach its band within <see cref="HorizonHours"/>.
///
/// Deterministic and pure — same shape as <see cref="ThresholdCheck"/> (T1) and
/// consistent with the hybrid design (Decision D12): the numbers that matter
/// (rate, ETA, confidence) come from arithmetic, not an LLM guess. Only a sensor
/// T1 already reports in spec is considered — a sensor already breaching is T1's
/// job, not T0's, so the two never double-signal the same condition.
/// </summary>
public static class TrendCheck
{
    /// <summary>Need at least this many points (recent + current) to fit a trend at all.</summary>
    public const int MinPoints = 3;

    /// <summary>Only warn about a breach projected within this many hours.</summary>
    public const double HorizonHours = 24.0;

    /// <summary>Minimum R² (goodness of fit) for a trend to be reported — filters sensor noise.</summary>
    public const double MinConfidence = 0.6;

    public static TrendReport Evaluate(
        Reading reading,
        IReadOnlyList<Reading> recent,
        Machine machine,
        ThresholdReport t1)
    {
        if (reading.MachineId != machine.Id)
            throw new ArgumentException($"Reading is for '{reading.MachineId}', not '{machine.Id}'.");

        // Oldest → newest, current reading last. `recent` comes back newest-first.
        var series = recent.Reverse().Append(reading).ToList();

        var warnings = new List<TrendWarning>();
        if (series.Count >= MinPoints)
        {
            foreach (var kind in Enum.GetValues<SensorKind>())
            {
                // Only chase sensors T1 says are still in spec — an active breach is T1's signal, not T0's.
                var evaluation = t1.Sensors.First(s => s.Sensor == kind);
                if (!evaluation.InSpec) continue;

                var warning = EvaluateSensor(kind, series, machine.Band(kind), reading.CapturedAt);
                if (warning is not null) warnings.Add(warning);
            }
        }

        var trace = BuildTrace(reading.Id, machine.Id, warnings, series.Count);
        return new TrendReport(reading.Id, machine.Id, warnings, trace);
    }

    private static TrendWarning? EvaluateSensor(
        SensorKind kind, IReadOnlyList<Reading> series, SensorBand band, DateTimeOffset now)
    {
        // x = hours relative to "now" (the current reading), so the fitted intercept
        // is the smoothed current value and the fitted line projects forward from x=0.
        var points = series
            .Select(r => (x: (r.CapturedAt - now).TotalHours, y: r.Value(kind)))
            .ToList();

        if (!TryFitLine(points, out var slope, out var intercept, out var r2))
            return null;
        if (r2 < MinConfidence) return null;

        var width = band.Max - band.Min;
        if (Math.Abs(width) < 1e-9) return null;   // degenerate band — nothing to approach

        // Rising toward Max, or falling toward Min. Anything else isn't heading for trouble.
        double bound;
        if (slope > 0) bound = band.Max;
        else if (slope < 0) bound = band.Min;
        else return null;

        if (Math.Abs(slope) < 1e-9) return null;

        var hoursToBreach = (bound - intercept) / slope;
        if (hoursToBreach <= 0 || hoursToBreach > HorizonHours) return null;

        return new TrendWarning(
            Sensor: kind,
            CurrentValue: series[^1].Value(kind),
            Unit: band.Unit,
            RateOfChangePerHour: Math.Round(slope, 4),
            BoundApproached: bound,
            ProjectedBreachAt: now.AddHours(hoursToBreach),
            HoursToBreachAt: Math.Round(hoursToBreach, 1),
            Confidence: Math.Round(r2, 2),
            SampleCount: points.Count);
    }

    /// <summary>Ordinary least squares. False if the fit is degenerate (e.g. all points at one x).</summary>
    private static bool TryFitLine(IReadOnlyList<(double x, double y)> points, out double slope, out double intercept, out double r2)
    {
        slope = intercept = r2 = 0;
        var n = points.Count;
        var sumX = points.Sum(p => p.x);
        var sumY = points.Sum(p => p.y);
        var sumXY = points.Sum(p => p.x * p.y);
        var sumXX = points.Sum(p => p.x * p.x);

        var denominator = n * sumXX - sumX * sumX;
        if (Math.Abs(denominator) < 1e-9) return false;

        slope = (n * sumXY - sumX * sumY) / denominator;
        intercept = (sumY - slope * sumX) / n;

        var meanY = sumY / n;
        var ssTot = points.Sum(p => (p.y - meanY) * (p.y - meanY));
        if (ssTot < 1e-9) return false;   // flat line — no variance to explain, not a "trend"

        var fittedSlope = slope;
        var fittedIntercept = intercept;
        var ssRes = points.Sum(p => (p.y - (fittedSlope * p.x + fittedIntercept)) * (p.y - (fittedSlope * p.x + fittedIntercept)));
        r2 = 1 - ssRes / ssTot;
        return true;
    }

    private static string BuildTrace(string readingId, string machineId, IReadOnlyList<TrendWarning> warnings, int sampleCount)
    {
        var sb = new StringBuilder($"T0 {readingId} {machineId}: ");
        if (warnings.Count == 0)
        {
            sb.Append(sampleCount < MinPoints
                ? $"insufficient history ({sampleCount}/{MinPoints}) — no trend evaluated"
                : "no early-warning trend");
        }
        else
        {
            sb.Append(string.Join("; ", warnings.Select(w =>
                $"{w.Sensor.Slug()} {w.CurrentValue}{w.Unit} trending {(w.RateOfChangePerHour > 0 ? "up" : "down")} " +
                $"{Math.Abs(w.RateOfChangePerHour)}{w.Unit}/h — projected to cross {w.BoundApproached}{w.Unit} " +
                $"in ~{w.HoursToBreachAt}h (R²={w.Confidence})")));
        }
        return sb.ToString();
    }
}
