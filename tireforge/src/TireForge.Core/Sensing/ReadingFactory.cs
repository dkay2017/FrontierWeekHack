using TireForge.Core.Model;

namespace TireForge.Core.Sensing;

/// <summary>
/// Synthesises a sensor <see cref="Reading"/> for a machine (Build Plan Stage B).
/// Pure — takes an explicit clock and RNG so tests are deterministic. The Sensor
/// Simulator function (Ingestion layer) is a thin wrapper over this.
/// </summary>
public static class ReadingFactory
{
    /// <summary>A <see cref="ReadingMode.Warn"/> breach sits this far past a band edge.</summary>
    public const double WarnDeviation = 0.10;

    /// <summary>A <see cref="ReadingMode.Crit"/> breach sits this far past a band edge.</summary>
    public const double CritDeviation = 0.60;

    /// <param name="machine">Machine whose bands shape the reading.</param>
    /// <param name="mode">normal → all sensors in band; warn/crit → one sensor pushed out.</param>
    /// <param name="at">Capture time (also seeds the reading id).</param>
    /// <param name="rng">Injected for deterministic tests. Defaults to <see cref="Random.Shared"/>.</param>
    public static Reading Make(Machine machine, ReadingMode mode, DateTimeOffset at, Random? rng = null)
    {
        rng ??= Random.Shared;

        // Every sensor starts in-band.
        var values = new Dictionary<SensorKind, double>
        {
            [SensorKind.Temperature] = InBand(machine.Temperature, rng),
            [SensorKind.Pressure] = InBand(machine.Pressure, rng),
            [SensorKind.Vibration] = InBand(machine.Vibration, rng),
            [SensorKind.Rpm] = InBand(machine.Rpm, rng),
        };

        if (mode != ReadingMode.Normal)
        {
            var deviation = mode == ReadingMode.Crit ? CritDeviation : WarnDeviation;
            var target = (SensorKind)rng.Next(4);
            values[target] = OutOfBand(machine.Band(target), deviation, rng);
        }

        return new Reading
        {
            Id = Ids.Reading(at),
            MachineId = machine.Id,
            CapturedAt = at,
            Temperature = values[SensorKind.Temperature],
            Pressure = values[SensorKind.Pressure],
            Vibration = values[SensorKind.Vibration],
            Rpm = values[SensorKind.Rpm],
            IsAnomaly = null,
            Mode = mode,
        };
    }

    private static double InBand(SensorBand band, Random rng)
    {
        if (band.Max <= band.Min)
            return band.Min;

        // Keep a 10% margin inside the band so "normal" never sits on an edge.
        var margin = (band.Max - band.Min) * 0.1;
        return Round(band.Min + margin + rng.NextDouble() * (band.Max - band.Min - 2 * margin));
    }

    private static double OutOfBand(SensorBand band, double deviation, Random rng)
    {
        var scale = Math.Max(Math.Abs(band.Max), Math.Max(band.Max - band.Min, 1.0));
        var push = scale * deviation * (0.85 + rng.NextDouble() * 0.3);
        return Round(band.Max + push);
    }

    private static double Round(double v) => Math.Round(v, 2);
}
