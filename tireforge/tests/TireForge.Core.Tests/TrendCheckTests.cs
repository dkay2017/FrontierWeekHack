using TireForge.Core.Model;
using TireForge.Core.Thresholds;
using TireForge.Core.Trends;

namespace TireForge.Core.Tests;

/// <summary>T0 — the predictive early-warning check. Pure arithmetic, no LLM.</summary>
public class TrendCheckTests
{
    private static readonly DateTimeOffset Now = new(2026, 5, 13, 12, 0, 0, TimeSpan.Zero);

    // Mixer: vibration band 0..4.5 mm/s. Everything else pinned mid-band so only
    // vibration is ever in play.
    private static Machine Mixer() => TestMachines.Mixer();

    private static Reading Reading(string id, double hoursFromNow, double vibration, double temperature = 75) => new()
    {
        Id = id,
        MachineId = "MX-001",
        CapturedAt = Now.AddHours(hoursFromNow),
        Temperature = temperature, Pressure = 3.0, Vibration = vibration, Rpm = 50,
    };

    private static (Reading current, IReadOnlyList<Reading> recent) Series(params double[] vibrationOldestToNewest)
    {
        var readings = vibrationOldestToNewest
            .Select((v, i) => Reading($"rdg-{i}", i - (vibrationOldestToNewest.Length - 1), v))
            .ToList();
        var current = readings[^1];
        var recent = readings.Take(readings.Count - 1).Reverse().ToList(); // newest-first, like RecentAsync
        return (current, recent);
    }

    private static (Reading current, IReadOnlyList<Reading> recent) TemperatureSeries(params double[] temperatureOldestToNewest)
    {
        var readings = temperatureOldestToNewest
            .Select((t, i) => Reading($"rdg-{i}", i - (temperatureOldestToNewest.Length - 1), vibration: 1.0, temperature: t))
            .ToList();
        var current = readings[^1];
        var recent = readings.Take(readings.Count - 1).Reverse().ToList();
        return (current, recent);
    }

    private static TrendReport Evaluate(Reading current, IReadOnlyList<Reading> recent, Machine machine)
    {
        var t1 = ThresholdCheck.Evaluate(current, machine);
        return TrendCheck.Evaluate(current, recent, machine, t1);
    }

    [Fact]
    public void Steady_linear_rise_projects_a_breach_within_the_horizon()
    {
        // Perfect line: 3.0, 3.3, 3.6, 3.9 mm/s an hour apart. Band max 4.5.
        // Slope 0.3/h, current 3.9 → (4.5-3.9)/0.3 = 2.0h to breach.
        var (current, recent) = Series(3.0, 3.3, 3.6, 3.9);
        var report = Evaluate(current, recent, Mixer());

        Assert.True(report.AnyWarning);
        var w = Assert.Single(report.Warnings);
        Assert.Equal(SensorKind.Vibration, w.Sensor);
        Assert.Equal(0.3, w.RateOfChangePerHour, 3);
        Assert.Equal(4.5, w.BoundApproached);
        Assert.Equal(2.0, w.HoursToBreachAt, 1);
        Assert.Equal(1.0, w.Confidence, 2);   // perfect fit
        Assert.Contains("trending up", report.Trace);
    }

    [Fact]
    public void Flat_readings_report_no_trend()
    {
        var (current, recent) = Series(3.0, 3.0, 3.0, 3.0);
        var report = Evaluate(current, recent, Mixer());

        Assert.False(report.AnyWarning);
        Assert.Contains("no early-warning trend", report.Trace);
    }

    [Fact]
    public void Cooling_back_down_from_near_max_is_improving_not_a_warning()
    {
        // Temperature band 60..90. Was near the top and is falling — that's
        // recovering, and Min (60) is nowhere near within the horizon at this rate.
        var (current, recent) = TemperatureSeries(85.9, 85.6, 85.3, 85.0);
        var report = Evaluate(current, recent, Mixer());

        Assert.False(report.AnyWarning);
    }

    [Fact]
    public void Falling_toward_the_low_bound_is_also_a_warning()
    {
        // The concern isn't only "rising toward Max" — a value sliding toward Min
        // (e.g. a pressure drop suggesting a leak) is exactly as much an early
        // warning. Temperature band 60..90, falling 0.3/h from 61.9 → ~6.3h to Min.
        var (current, recent) = TemperatureSeries(62.8, 62.5, 62.2, 61.9);
        var report = Evaluate(current, recent, Mixer());

        Assert.True(report.AnyWarning);
        var w = Assert.Single(report.Warnings);
        Assert.Equal(SensorKind.Temperature, w.Sensor);
        Assert.Equal(60, w.BoundApproached);
        Assert.Contains("trending down", report.Trace);
    }

    [Fact]
    public void Noisy_readings_with_a_weak_fit_are_not_reported()
    {
        // Same overall direction as the rising case, but too much noise to trust —
        // R² should fall below MinConfidence.
        var (current, recent) = Series(3.0, 3.8, 3.1, 3.9);
        var report = Evaluate(current, recent, Mixer());

        Assert.False(report.AnyWarning);
    }

    [Fact]
    public void Projected_breach_beyond_the_horizon_is_not_reported()
    {
        // Same slope shape as the steady-rise case but starting far from the bound:
        // 0.003 mm/s/h means 500+ hours to breach — well past the 24h horizon.
        var (current, recent) = Series(1.000, 1.003, 1.006, 1.009);
        var report = Evaluate(current, recent, Mixer());

        Assert.False(report.AnyWarning);
    }

    [Fact]
    public void Insufficient_history_is_not_evaluated()
    {
        var (current, recent) = Series(3.6, 3.9);   // only 2 points total
        var report = Evaluate(current, recent, Mixer());

        Assert.False(report.AnyWarning);
        Assert.Contains("insufficient history", report.Trace);
    }

    [Fact]
    public void An_already_breaching_sensor_is_skipped_by_T0_not_double_signalled()
    {
        // Vibration is already over the 4.5 max — T1 owns this, T0 must stay silent
        // on it even though the trend keeps climbing.
        var (current, recent) = Series(4.6, 4.9, 5.2, 5.5);
        var report = Evaluate(current, recent, Mixer());

        Assert.False(report.AnyWarning);
        var t1 = ThresholdCheck.Evaluate(current, Mixer());
        Assert.True(t1.Sensors.Single(s => s.Sensor == SensorKind.Vibration).Status == SensorStatus.High);
    }
}
