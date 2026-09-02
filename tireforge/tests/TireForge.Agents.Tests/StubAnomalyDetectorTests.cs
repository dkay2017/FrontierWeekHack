using TireForge.Agents.Anomaly;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Tests;

/// <summary>Build Plan Stage D checks (A1 stub).</summary>
public class StubAnomalyDetectorTests
{
    private static readonly Machine Mixer = new()
    {
        Id = "MX-001", Name = "mixer",
        Temperature = Band(60, 90, "celsius"),
        Pressure = Band(2.0, 4.0, "bar"),
        Vibration = Band(0, 4.5, "mm/s"),
        Rpm = Band(40, 65, "rpm"),
    };

    private readonly IAnomalyDetector _detector = new StubAnomalyDetector();

    [Fact]
    public async Task Out_of_band_reading_is_flagged_and_cites_the_reading()
    {
        var reading = Reading(92.3, 3.1, 4.8, 58); // temp + vibration high
        var verdict = await _detector.DetectAsync(reading, ThresholdCheck.Evaluate(reading, Mixer), Array.Empty<Reading>());

        Assert.True(verdict.IsAnomaly);
        Assert.Equal(new[] { reading.Id }, verdict.Cites);
        Assert.Contains(reading.Id, verdict.Text);
        Assert.Contains("anomaly", verdict.Text);
    }

    [Fact]
    public async Task In_spec_reading_is_not_flagged()
    {
        var reading = Reading(75, 3.0, 2.0, 55);
        var verdict = await _detector.DetectAsync(reading, ThresholdCheck.Evaluate(reading, Mixer), Array.Empty<Reading>());

        Assert.False(verdict.IsAnomaly);
        Assert.Contains("no anomaly", verdict.Text);
    }

    [Fact]
    public async Task ApplyTo_writes_is_anomaly_back_onto_the_reading()
    {
        var reading = Reading(92.3, 3.1, 4.8, 58);
        Assert.Null(reading.IsAnomaly);

        var verdict = await _detector.DetectAsync(reading, ThresholdCheck.Evaluate(reading, Mixer), Array.Empty<Reading>());
        verdict.ApplyTo(reading);

        Assert.True(reading.IsAnomaly);
    }

    [Fact]
    public async Task Rejects_a_t1_report_for_a_different_reading()
    {
        var a = Reading(92.3, 3.1, 4.8, 58);
        var b = Reading(75, 3.0, 2.0, 55);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _detector.DetectAsync(a, ThresholdCheck.Evaluate(b, Mixer), Array.Empty<Reading>()));
    }

    private static Reading Reading(double t, double p, double v, double r) => new()
    {
        Id = Ids.Reading(DateTimeOffset.UtcNow),
        MachineId = "MX-001",
        CapturedAt = DateTimeOffset.UtcNow,
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };

    private static SensorBand Band(double min, double max, string unit) => new() { Min = min, Max = max, Unit = unit };
}
