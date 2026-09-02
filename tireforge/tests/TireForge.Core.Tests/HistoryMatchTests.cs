using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Tests;

/// <summary>Build Plan Stage E checks.</summary>
public class HistoryMatchTests
{
    private static ThresholdReport T1(string machineId) =>
        ThresholdCheck.Evaluate(TestMachines.Snapshot(machineId), TestMachines.All().Single(m => m.Id == machineId));

    [Fact]
    public void Signature_is_canonical_sorted_tokens()
    {
        var sig = FaultSignature.From(T1("CP-003")); // temp, pressure, vibration all high
        Assert.Equal("pressure-high+temperature-high+vibration-high", sig);
    }

    [Fact]
    public async Task Exact_signature_match_hits_seed_history()
    {
        var machine = TestMachines.Mixer();
        // Force a single-sensor temperature breach → signature "temperature-high".
        var reading = TestMachines.Snapshot("MX-001");
        reading.Vibration = machine.Vibration.Max - 0.1; // pull vibration back in band

        var report = await HistoryMatch.RunAsync(machine, ThresholdCheck.Evaluate(reading, machine), new FakeHistoryStore());

        Assert.True(report.Exact);
        Assert.Equal(new[] { "inc-001" }, report.Cites);
        Assert.Contains("T2 rdg-seed-MX-001 MX-001: signature 'temperature-high'", report.Trace);
    }

    [Fact]
    public async Task Falls_back_to_best_token_overlap_when_no_exact_match()
    {
        // CP-003 snapshot: pressure+temp+vibration high — no exact seed row.
        var report = await HistoryMatch.RunAsync(TestMachines.CuringPress(), T1("CP-003"), new FakeHistoryStore());

        Assert.False(report.Exact);
        Assert.NotEmpty(report.Incidents);
        Assert.Equal("inc-005", report.Cites[0]); // temperature-high+vibration-high — overlap 2
        Assert.Contains("closest match", report.Trace);
    }

    [Fact]
    public async Task No_breach_yields_empty_signature_and_no_incidents()
    {
        var report = await HistoryMatch.RunAsync(TestMachines.Extruder(), T1("EX-002"), new FakeHistoryStore());

        Assert.Equal("", report.Signature);
        Assert.False(report.AnyMatch);
        Assert.Contains("no prior incidents", report.Trace);
    }

    [Fact]
    public async Task Does_not_match_incidents_from_a_different_machine()
    {
        // IS-005 vibration-high exists as inc-008; MX-001 vibration-high is inc-002.
        var machine = TestMachines.InspectionStation();
        var report = await HistoryMatch.RunAsync(machine, T1("IS-005"), new FakeHistoryStore());

        Assert.All(report.Incidents, i => Assert.Equal("IS-005", i.MachineId));
        Assert.Equal(new[] { "inc-008" }, report.Cites);
    }

    [Fact]
    public async Task Rejects_a_t1_report_for_a_different_machine()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            HistoryMatch.RunAsync(TestMachines.Extruder(), T1("MX-001"), new FakeHistoryStore()));
    }
}
