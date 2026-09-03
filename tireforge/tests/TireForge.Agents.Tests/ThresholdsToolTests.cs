using System.Text.Json;
using TireForge.Agents.Foundry;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Tests;

/// <summary>The <c>check_thresholds</c> tool payload the anomaly agent gets back.</summary>
public class ThresholdsToolTests
{
    [Fact]
    public void Serializes_a_critical_reading_with_its_anomalies()
    {
        // CP-003 curing press — the seeded critical snapshot.
        var reading = Fx.Reading("CP-003", t: 198.5, p: 18.2, v: 7.3, r: 0);
        var t1 = ThresholdCheck.Evaluate(reading, Fx.CuringPress());

        using var doc = JsonDocument.Parse(ThresholdsTool.Serialize(t1));
        var root = doc.RootElement;

        Assert.Equal("CP-003", root.GetProperty("machine_id").GetString());
        Assert.Equal(reading.Id, root.GetProperty("reading_id").GetString());
        Assert.Equal("crit", root.GetProperty("severity").GetString());

        var anomalies = root.GetProperty("anomalies");
        Assert.Equal(3, anomalies.GetArrayLength()); // temp, pressure, vibration
        Assert.All(anomalies.EnumerateArray(),
            a => Assert.Contains("above max", a.GetProperty("deviation").GetString()));

        Assert.False(root.GetProperty("all_readings").GetProperty("temperature").GetProperty("in_spec").GetBoolean());
        Assert.True(root.GetProperty("all_readings").GetProperty("rpm").GetProperty("in_spec").GetBoolean());
    }

    [Fact]
    public void In_spec_reading_has_no_anomalies()
    {
        var reading = Fx.Reading("MX-001", t: 75, p: 3.0, v: 2.0, r: 50);
        var t1 = ThresholdCheck.Evaluate(reading, Fx.Mixer());

        using var doc = JsonDocument.Parse(ThresholdsTool.Serialize(t1));
        Assert.Equal(0, doc.RootElement.GetProperty("anomalies").GetArrayLength());
        Assert.Equal("info", doc.RootElement.GetProperty("severity").GetString());
    }
}
