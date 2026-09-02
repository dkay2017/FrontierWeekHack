using TireForge.Core.Model;
using TireForge.Core.Sensing;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Tests;

/// <summary>Build Plan Stage B checks.</summary>
public class ReadingFactoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Normal_reading_is_in_band_for_every_machine()
    {
        foreach (var machine in TestMachines.All())
        {
            var rng = new Random(42);
            var reading = ReadingFactory.Make(machine, ReadingMode.Normal, T0, rng);
            var report = ThresholdCheck.Evaluate(reading, machine);

            Assert.False(report.AnyBreach, $"{machine.Id} normal reading breached: {report.Trace}");
            Assert.Equal(Severity.Info, report.Severity);
        }
    }

    [Fact]
    public void Crit_reading_is_out_of_band()
    {
        foreach (var machine in TestMachines.All())
        {
            var rng = new Random(7);
            var reading = ReadingFactory.Make(machine, ReadingMode.Crit, T0, rng);
            var report = ThresholdCheck.Evaluate(reading, machine);

            Assert.True(report.AnyBreach, $"{machine.Id} crit reading stayed in band: {report.Trace}");
        }
    }

    [Fact]
    public void Warn_reading_breaches_exactly_one_sensor()
    {
        var machine = TestMachines.Mixer();
        var reading = ReadingFactory.Make(machine, ReadingMode.Warn, T0, new Random(1));
        var report = ThresholdCheck.Evaluate(reading, machine);

        Assert.Single(report.Breaches);
    }

    [Fact]
    public void Reading_id_is_prefixed_and_carries_the_mode()
    {
        var reading = ReadingFactory.Make(TestMachines.Extruder(), ReadingMode.Warn, T0, new Random(1));

        Assert.StartsWith("rdg-", reading.Id);
        Assert.Equal(ReadingMode.Warn, reading.Mode);
        Assert.Null(reading.IsAnomaly);
    }

    [Fact]
    public void Reading_ids_sort_chronologically_as_strings()
    {
        // One reading per 100ms, as a live sensor would emit them.
        var ids = Enumerable.Range(0, 100)
            .Select(i => Ids.Reading(T0.AddMilliseconds(100 * i)))
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
        Assert.Equal(ids.OrderBy(x => x, StringComparer.Ordinal), ids);
    }
}
