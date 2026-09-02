using TireForge.Agents;
using TireForge.Core.Agents;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Tests;

/// <summary>Build Plan Stage F checks (A2 stub) — incl. "some &lt; 0.70, some Crit".</summary>
public class StubFaultDiagnoserTests
{
    private readonly IFaultDiagnoser _a2 = new StubFaultDiagnoser();
    private static readonly AnomalyVerdict Anomalous = new(true, "A1 … anomaly", new[] { "rdg-x" });

    [Fact]
    public async Task Exact_history_match_names_the_prior_fault_with_high_confidence()
    {
        var m = Fx.InspectionStation();
        var reading = Fx.Reading("IS-005", 25, 1.0, 5.2, 1800); // vibration 5.2 > 4.0
        var t1 = ThresholdCheck.Evaluate(reading, m);
        var inc = Fx.Incident("inc-008", "IS-005", "vibration-high", "spindle bearing wear", Severity.Warn);
        var t2 = Fx.History(reading.Id, "IS-005", "vibration-high", exact: true, inc);

        var v = await _a2.DiagnoseAsync(reading, t1, t2, Anomalous);

        Assert.Equal("spindle bearing wear", v.Fault);
        Assert.True(v.Confidence >= 0.70, $"confidence was {v.Confidence}");
        Assert.Contains("inc-008", v.Cites);
        Assert.Contains(reading.Id, v.Cites);
    }

    [Fact]
    public async Task Compound_breach_is_critical_and_low_confidence()
    {
        var m = Fx.CuringPress();
        var reading = Fx.Reading("CP-003", 198.5, 18.2, 7.3, 0); // temp + pressure + vibration high
        var t1 = ThresholdCheck.Evaluate(reading, m);
        var t2 = Fx.History(reading.Id, "CP-003", "pressure-high+temperature-high+vibration-high", exact: false,
            Fx.Incident("inc-005", "CP-003", "temperature-high+vibration-high", "platen bearing failure", Severity.Crit));

        var v = await _a2.DiagnoseAsync(reading, t1, t2, Anomalous);

        Assert.Equal(Severity.Crit, v.Severity);
        Assert.True(v.Confidence < 0.70, $"confidence was {v.Confidence}");
        Assert.Contains("compound", v.Fault);
    }

    [Fact]
    public async Task Temp_and_vibration_high_uses_the_rubric_when_no_exact_match()
    {
        var m = Fx.Mixer();
        var reading = Fx.Reading("MX-001", 92.3, 3.1, 4.8, 58); // temp + vibration just over
        var t1 = ThresholdCheck.Evaluate(reading, m);
        var t2 = Fx.History(reading.Id, "MX-001", "temperature-high+vibration-high", exact: false,
            Fx.Incident("inc-002", "MX-001", "vibration-high", "mixing blade imbalance", Severity.Info));

        var v = await _a2.DiagnoseAsync(reading, t1, t2, Anomalous);

        Assert.Contains("bearing failure or lubrication", v.Fault);
        Assert.True(v.Confidence < 0.70);
    }

    [Fact]
    public async Task No_history_still_produces_a_verdict_from_the_pattern()
    {
        var m = Fx.Mixer();
        var reading = Fx.Reading("MX-001", 92.3, 3.0, 2.0, 58); // temp only
        var t1 = ThresholdCheck.Evaluate(reading, m);
        var t2 = Fx.NoHistory(reading.Id, "MX-001", "temperature-high");

        var v = await _a2.DiagnoseAsync(reading, t1, t2, Anomalous);

        Assert.False(string.IsNullOrWhiteSpace(v.Fault));
        Assert.Equal(new[] { reading.Id }, v.Cites);
    }

    [Fact]
    public async Task Refuses_to_diagnose_a_non_anomalous_reading()
    {
        var m = Fx.Mixer();
        var reading = Fx.Reading("MX-001", 75, 3.0, 2.0, 55);
        var t1 = ThresholdCheck.Evaluate(reading, m);
        var t2 = Fx.NoHistory(reading.Id, "MX-001", "");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _a2.DiagnoseAsync(reading, t1, t2, new AnomalyVerdict(false, "no anomaly", new[] { reading.Id })));
    }

    [Fact]
    public async Task Across_the_three_anomalous_snapshots_there_is_a_spread()
    {
        var results = new List<FaultVerdict>();

        var mx = Fx.Reading("MX-001", 92.3, 3.1, 4.8, 58);
        results.Add(await _a2.DiagnoseAsync(mx, ThresholdCheck.Evaluate(mx, Fx.Mixer()),
            Fx.History(mx.Id, "MX-001", "temperature-high+vibration-high", false,
                Fx.Incident("inc-001", "MX-001", "temperature-high", "overheating", Severity.Warn)), Anomalous));

        var is5 = Fx.Reading("IS-005", 25, 1.0, 5.2, 1800);
        results.Add(await _a2.DiagnoseAsync(is5, ThresholdCheck.Evaluate(is5, Fx.InspectionStation()),
            Fx.History(is5.Id, "IS-005", "vibration-high", true,
                Fx.Incident("inc-008", "IS-005", "vibration-high", "spindle bearing wear", Severity.Warn)), Anomalous));

        var cp = Fx.Reading("CP-003", 198.5, 18.2, 7.3, 0);
        results.Add(await _a2.DiagnoseAsync(cp, ThresholdCheck.Evaluate(cp, Fx.CuringPress()),
            Fx.History(cp.Id, "CP-003", "pressure-high+temperature-high+vibration-high", false), Anomalous));

        Assert.Contains(results, r => r.Confidence < 0.70);
        Assert.Contains(results, r => r.Confidence >= 0.70);
        Assert.Contains(results, r => r.Severity == Severity.Crit);
    }
}
