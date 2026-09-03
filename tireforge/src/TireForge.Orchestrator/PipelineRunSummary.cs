using TireForge.Core.Pipeline;

namespace TireForge.Orchestrator;

/// <summary>
/// The serialisable result of one durable pipeline run — what the orchestration
/// returns and what shows in the Durable instance history / portal.
/// </summary>
public sealed record PipelineRunSummary(
    string TraceId,
    string ReadingId,
    string MachineId,
    bool IsAnomaly,
    string ThresholdSeverity,
    string? DiagnosisId,
    string? Fault,
    double? Confidence,
    string? GateRoute,
    string? WorkOrderId,
    IReadOnlyList<string> Trace)
{
    public static PipelineRunSummary From(PipelineResult r) => new(
        r.TraceId,
        r.ReadingId,
        r.MachineId,
        r.IsAnomaly,
        r.ThresholdSeverity.ToString(),
        r.Diagnosis?.Id,
        r.Diagnosis?.Fault,
        r.Diagnosis?.Confidence,
        r.Diagnosis is null ? null : r.Act?.Route.ToString(),
        r.WorkOrder?.Id,
        r.Trace);
}
