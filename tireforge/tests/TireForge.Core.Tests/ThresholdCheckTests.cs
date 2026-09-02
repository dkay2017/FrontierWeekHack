using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Tests;

/// <summary>Build Plan Stage C checks — incl. the Challenge 1 success criteria.</summary>
public class ThresholdCheckTests
{
    [Theory]
    [InlineData("MX-001", Severity.Warn)]   // temp + vibration just over — 2 breaches, worst ~6.7%
    [InlineData("EX-002", Severity.Info)]   // all in spec
    [InlineData("CP-003", Severity.Crit)]   // vibration 143% over — critical
    [InlineData("CU-004", Severity.Info)]
    [InlineData("IS-005", Severity.Warn)]   // vibration 30% over — 1 breach
    public void Snapshot_readings_get_the_Challenge1_severity(string machineId, Severity expected)
    {
        var machine = TestMachines.All().Single(m => m.Id == machineId);
        var report = ThresholdCheck.Evaluate(TestMachines.Snapshot(machineId), machine);

        Assert.Equal(expected, report.Severity);
    }

    [Fact]
    public void Challenge1_flags_exactly_two_warnings_and_one_critical()
    {
        var reports = TestMachines.All()
            .Select(m => ThresholdCheck.Evaluate(TestMachines.Snapshot(m.Id), m))
            .ToList();

        Assert.Equal(2, reports.Count(r => r.Severity == Severity.Warn));
        Assert.Equal(1, reports.Count(r => r.Severity == Severity.Crit));
    }

    [Fact]
    public void High_sensor_reports_status_and_deviation_above_max()
    {
        var machine = TestMachines.Mixer(); // vibration band 0..4.5
        var report = ThresholdCheck.Evaluate(TestMachines.Snapshot("MX-001"), machine);

        var vib = report.Sensors.Single(s => s.Sensor == SensorKind.Vibration);
        Assert.Equal(SensorStatus.High, vib.Status);
        Assert.Equal(6.7, vib.DeviationPct, 1); // (4.8 - 4.5) / 4.5 * 100
    }

    [Fact]
    public void Low_sensor_reports_status_and_deviation_below_min()
    {
        var machine = TestMachines.Mixer(); // rpm band 40..65
        var reading = TestMachines.Snapshot("MX-001");
        reading.Rpm = 30;

        var rpm = ThresholdCheck.Evaluate(reading, machine).Sensors.Single(s => s.Sensor == SensorKind.Rpm);
        Assert.Equal(SensorStatus.Low, rpm.Status);
        Assert.Equal(25.0, rpm.DeviationPct, 1); // (40 - 30) / 40 * 100
    }

    [Fact]
    public void Degenerate_zero_band_does_not_divide_by_zero()
    {
        var machine = TestMachines.CuringPress(); // rpm band 0..0
        var reading = TestMachines.Snapshot("CP-003");
        reading.Rpm = 5;

        var rpm = ThresholdCheck.Evaluate(reading, machine).Sensors.Single(s => s.Sensor == SensorKind.Rpm);
        Assert.Equal(SensorStatus.High, rpm.Status);
        Assert.True(double.IsFinite(rpm.DeviationPct));
    }

    [Fact]
    public void Three_breaches_escalates_to_critical_regardless_of_deviation_size()
    {
        var machine = TestMachines.CoolingUnit();
        var reading = TestMachines.Snapshot("CU-004");
        reading.Temperature = machine.Temperature.Max + 1;
        reading.Pressure = machine.Pressure.Max + 0.05;
        reading.Vibration = machine.Vibration.Max + 0.05;

        var report = ThresholdCheck.Evaluate(reading, machine);
        Assert.Equal(3, report.Breaches.Count);
        Assert.Equal(Severity.Crit, report.Severity);
    }

    [Fact]
    public void Trace_line_cites_the_reading_id_and_offending_sensor()
    {
        var report = ThresholdCheck.Evaluate(TestMachines.Snapshot("CP-003"), TestMachines.CuringPress());

        Assert.StartsWith("T1 rdg-seed-CP-003 CP-003:", report.Trace);
        Assert.Contains("vibration", report.Trace);
        Assert.Contains("severity=Crit", report.Trace);
    }

    [Fact]
    public void Evaluate_rejects_a_reading_for_a_different_machine()
    {
        Assert.Throws<ArgumentException>(() =>
            ThresholdCheck.Evaluate(TestMachines.Snapshot("MX-001"), TestMachines.Extruder()));
    }

    [Fact]
    public void Breaches_are_ordered_worst_first()
    {
        var report = ThresholdCheck.Evaluate(TestMachines.Snapshot("CP-003"), TestMachines.CuringPress());

        var devs = report.Breaches.Select(b => b.DeviationPct).ToList();
        Assert.Equal(devs.OrderByDescending(d => d), devs);
    }
}
