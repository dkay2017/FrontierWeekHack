using System.Text;
using TireForge.Core.Model;

namespace TireForge.Core.Thresholds;

public enum SensorStatus
{
    Ok,
    Low,
    High,
}

/// <summary>One sensor's standing against its band.</summary>
public sealed record SensorEvaluation(
    SensorKind Sensor,
    double Value,
    double Min,
    double Max,
    string Unit,
    SensorStatus Status,
    double DeviationPct)
{
    public bool InSpec => Status == SensorStatus.Ok;
}

/// <summary>Result of ThresholdCheck / T1 for one reading (Build Plan Stage C).</summary>
public sealed record ThresholdReport(
    string ReadingId,
    string MachineId,
    Severity Severity,
    IReadOnlyList<SensorEvaluation> Sensors,
    string Trace)
{
    public IReadOnlyList<SensorEvaluation> Breaches =>
        Sensors.Where(s => !s.InSpec).OrderByDescending(s => s.DeviationPct).ToList();

    public bool AnyBreach => Sensors.Any(s => !s.InSpec);
}

/// <summary>
/// Pure threshold evaluation — the C# equivalent of Challenge 1's
/// <c>check_thresholds</c> tool, plus a seeded severity and a citing trace line.
/// </summary>
public static class ThresholdCheck
{
    /// <summary>Worst single-sensor deviation at/above this (percent) makes the reading critical.</summary>
    public const double CritDeviationPct = 50.0;

    /// <summary>This many sensors out of band at once makes the reading critical.</summary>
    public const int CritBreachCount = 3;

    public static ThresholdReport Evaluate(Reading reading, Machine machine)
    {
        if (reading.MachineId != machine.Id)
            throw new ArgumentException($"Reading is for '{reading.MachineId}', not '{machine.Id}'.");

        var sensors = new[]
        {
            SensorKind.Temperature, SensorKind.Pressure, SensorKind.Vibration, SensorKind.Rpm,
        }.Select(kind => Evaluate(kind, reading.Value(kind), machine.Band(kind))).ToList();

        var severity = SeverityFrom(sensors);
        var trace = BuildTrace(reading.Id, machine.Id, sensors, severity);
        return new ThresholdReport(reading.Id, machine.Id, severity, sensors, trace);
    }

    private static SensorEvaluation Evaluate(SensorKind kind, double value, SensorBand band)
    {
        SensorStatus status;
        double deviationPct;

        if (value > band.Max)
        {
            status = SensorStatus.High;
            deviationPct = (value - band.Max) / Denominator(band.Max, band) * 100.0;
        }
        else if (value < band.Min)
        {
            status = SensorStatus.Low;
            deviationPct = (band.Min - value) / Denominator(band.Min, band) * 100.0;
        }
        else
        {
            status = SensorStatus.Ok;
            deviationPct = 0;
        }

        return new SensorEvaluation(kind, value, band.Min, band.Max, band.Unit, status, Math.Round(deviationPct, 1));
    }

    // Challenge 1 divides by the violated bound; guard the degenerate cases
    // (a [0,0] band like CP-003 rpm) by falling back to band width, then 1 unit.
    private static double Denominator(double bound, SensorBand band)
    {
        if (Math.Abs(bound) > 1e-9) return Math.Abs(bound);
        var width = band.Max - band.Min;
        return Math.Abs(width) > 1e-9 ? Math.Abs(width) : 1.0;
    }

    private static Severity SeverityFrom(IReadOnlyList<SensorEvaluation> sensors)
    {
        var breaches = sensors.Where(s => !s.InSpec).ToList();
        if (breaches.Count == 0)
            return Severity.Info;

        var worst = breaches.Max(s => s.DeviationPct);
        if (worst >= CritDeviationPct || breaches.Count >= CritBreachCount)
            return Severity.Crit;

        return Severity.Warn;
    }

    private static string BuildTrace(
        string readingId, string machineId, IReadOnlyList<SensorEvaluation> sensors, Severity severity)
    {
        var breaches = sensors.Where(s => !s.InSpec).OrderByDescending(s => s.DeviationPct).ToList();
        var sb = new StringBuilder($"T1 {readingId} {machineId}: ");

        if (breaches.Count == 0)
        {
            sb.Append("all sensors in spec");
        }
        else
        {
            sb.Append(string.Join("; ", breaches.Select(b =>
            {
                var edge = b.Status == SensorStatus.High ? $"> {b.Max}" : $"< {b.Min}";
                var dir = b.Status == SensorStatus.High ? "above max" : "below min";
                return $"{b.Sensor.Slug()} {b.Value} {b.Unit} {edge} ({b.DeviationPct}% {dir})";
            })));
        }

        sb.Append($" — severity={severity}");
        return sb.ToString();
    }
}
