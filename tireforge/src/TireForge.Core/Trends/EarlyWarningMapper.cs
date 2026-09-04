using TireForge.Core.Model;

namespace TireForge.Core.Trends;

/// <summary>Assembles a persistable <see cref="EarlyWarning"/> row from one T0 <see cref="TrendWarning"/>.</summary>
public static class EarlyWarningMapper
{
    public static EarlyWarning ToEntity(TrendWarning warning, Reading reading, string traceId, DateTimeOffset at)
        => new()
        {
            Id = Ids.EarlyWarning(at),
            ReadingId = reading.Id,
            MachineId = reading.MachineId,
            Sensor = warning.Sensor,
            CurrentValue = warning.CurrentValue,
            Unit = warning.Unit,
            RateOfChangePerHour = warning.RateOfChangePerHour,
            BoundApproached = warning.BoundApproached,
            ProjectedBreachAt = warning.ProjectedBreachAt,
            HoursToBreachAt = warning.HoursToBreachAt,
            Confidence = warning.Confidence,
            NarrativeText = Narrate(warning, reading),
            Status = EarlyWarningStatus.Open,
            TraceId = traceId,
            RaisedAt = at,
        };

    /// <summary>
    /// Deterministic narrative — always available, no agent round-trip required.
    /// <see cref="Agents.IEarlyWarningNarrator"/> (Foundry, predictive-maintenance-agent)
    /// can replace this with a richer write-up when that path is enabled; until then
    /// the dashboard and API show this text.
    /// </summary>
    private static string Narrate(TrendWarning w, Reading reading)
    {
        var direction = w.RateOfChangePerHour > 0 ? "rising" : "falling";
        return $"{reading.MachineId}: {w.Sensor.Slug()} is {direction} " +
               $"{Math.Abs(w.RateOfChangePerHour):0.###} {w.Unit}/h from {w.CurrentValue}{w.Unit}. " +
               $"At this rate it crosses {w.BoundApproached}{w.Unit} in about {w.HoursToBreachAt:0.#}h " +
               $"(confidence {w.Confidence:P0} — {w.SampleCount} recent readings). Nothing has broken yet; " +
               $"this is a heads-up to schedule a look before it does.";
    }
}
