using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Agents;

// Agent ports + their structured outputs. The pure Core pipeline depends on these;
// TireForge.Agents supplies the stub and (Stage M) the real Foundry implementations.

/// <summary>Anomaly Detection agent — A1 (Build Plan Stage D).</summary>
public interface IAnomalyDetector
{
    Task<AnomalyVerdict> DetectAsync(
        Reading reading, ThresholdReport t1, IReadOnlyList<Reading> recent, CancellationToken ct = default);
}

/// <summary>A1 output — <c>{is_anomaly, text, cites}</c>. The text always cites the reading id.</summary>
public sealed record AnomalyVerdict(bool IsAnomaly, string Text, IReadOnlyList<string> Cites)
{
    /// <summary>Write the verdict back onto the reading (Build Plan Stage D step 3).</summary>
    public void ApplyTo(Reading reading) => reading.IsAnomaly = IsAnomaly;
}

/// <summary>Fault Diagnosis agent — A2 (Build Plan Stage F).</summary>
public interface IFaultDiagnoser
{
    Task<FaultVerdict> DiagnoseAsync(
        Reading reading, ThresholdReport t1, HistoryReport t2, AnomalyVerdict a1, CancellationToken ct = default);
}

/// <summary>A2 structured output — <c>{fault, severity, confidence 0–1, text, cites:[rdg,inc]}</c>.</summary>
public sealed record FaultVerdict
{
    public required string Fault { get; init; }
    public required Severity Severity { get; init; }

    /// <summary>Model confidence, 0–1. Drives the Gate (invariant 1.3).</summary>
    public required double Confidence { get; init; }

    public required string Text { get; init; }

    /// <summary>Source records the verdict rests on: the reading id, then any incident ids.</summary>
    public required IReadOnlyList<string> Cites { get; init; }

    public FaultVerdict Validate()
    {
        if (string.IsNullOrWhiteSpace(Fault))
            throw new ArgumentException("FaultVerdict.Fault must be set.");
        if (Confidence is < 0 or > 1 || double.IsNaN(Confidence))
            throw new ArgumentOutOfRangeException(nameof(Confidence), Confidence, "Confidence must be within [0, 1].");
        if (Cites.Count == 0)
            throw new ArgumentException("FaultVerdict.Cites must cite at least the reading id.");
        return this;
    }
}

/// <summary>Work Order agent — A3 (Build Plan Stage H).</summary>
public interface IWorkOrderDrafter
{
    Task<WorkOrderDraft> DraftAsync(Diagnosis diagnosis, Machine machine, CancellationToken ct = default);
}

/// <summary>A3 output — <c>{machine, fault, severity, reading_id, action_text}</c>. Cites the reading.</summary>
public sealed record WorkOrderDraft(
    string MachineId, string Fault, Severity Severity, string ReadingId, string ActionText);
