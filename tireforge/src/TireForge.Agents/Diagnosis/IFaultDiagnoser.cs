using TireForge.Agents.Anomaly;
using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Diagnosis;

/// <summary>The Fault Diagnosis agent — A2 (Build Plan Stage F).</summary>
public interface IFaultDiagnoser
{
    Task<FaultVerdict> DiagnoseAsync(
        Reading reading,
        ThresholdReport t1,
        HistoryReport t2,
        AnomalyVerdict a1,
        CancellationToken ct = default);
}

/// <summary>
/// A2 structured output — <c>{fault, severity, confidence 0–1, text, cites:[rdg,inc]}</c>
/// (Build Plan Stage F step 1). <see cref="Validate"/> enforces the schema.
/// </summary>
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
