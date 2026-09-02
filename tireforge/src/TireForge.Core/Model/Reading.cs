namespace TireForge.Core.Model;

/// <summary>
/// One sensor sample for a machine at a point in time (Build Plan Stage B).
/// </summary>
public class Reading
{
    /// <summary>Sortable id: <c>rdg-&lt;ticks&gt;-&lt;rand&gt;</c>.</summary>
    public required string Id { get; set; }

    public required string MachineId { get; set; }
    public Machine? Machine { get; set; }

    public DateTimeOffset CapturedAt { get; set; }

    public double Temperature { get; set; }
    public double Pressure { get; set; }
    public double Vibration { get; set; }
    public double Rpm { get; set; }

    /// <summary>Set by Anomaly Detection (Stage D). <c>null</c> until evaluated.</summary>
    public bool? IsAnomaly { get; set; }

    /// <summary>Generator mode when synthesised (Stage B). <c>null</c> for real telemetry.</summary>
    public ReadingMode? Mode { get; set; }

    public double Value(SensorKind kind) => kind switch
    {
        SensorKind.Temperature => Temperature,
        SensorKind.Pressure => Pressure,
        SensorKind.Vibration => Vibration,
        SensorKind.Rpm => Rpm,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
