namespace TireForge.Core.Model;

/// <summary>
/// A T0 predictive early warning (see <see cref="Trends.TrendCheck"/>) — a sensor
/// that is still in spec today but, at its current trajectory, is projected to
/// breach its band within the horizon. Advisory: no work order is drafted from
/// this alone (that stays the reviewer's or T1's call once it actually breaches).
/// </summary>
public class EarlyWarning
{
    /// <summary>Id: <c>ew-&lt;ticks&gt;-&lt;rand&gt;</c>.</summary>
    public required string Id { get; set; }

    public required string ReadingId { get; set; }

    public required string MachineId { get; set; }
    public Machine? Machine { get; set; }

    public SensorKind Sensor { get; set; }
    public double CurrentValue { get; set; }
    public string Unit { get; set; } = "";
    public double RateOfChangePerHour { get; set; }
    public double BoundApproached { get; set; }
    public DateTimeOffset ProjectedBreachAt { get; set; }
    public double HoursToBreachAt { get; set; }

    /// <summary>Goodness of fit (R²) of the trend line, 0–1.</summary>
    public double Confidence { get; set; }

    /// <summary>Human-readable summary — deterministic today (Core.Trends), agent-narrated later.</summary>
    public string NarrativeText { get; set; } = "";

    public EarlyWarningStatus Status { get; set; } = EarlyWarningStatus.Open;
    public string? ReviewerNote { get; set; }

    /// <summary>Correlated trace id shared with the reading's pipeline run.</summary>
    public string TraceId { get; set; } = "";

    public DateTimeOffset RaisedAt { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
}
