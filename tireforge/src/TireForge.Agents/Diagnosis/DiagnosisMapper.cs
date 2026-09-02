using TireForge.Agents.Anomaly;
using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Agents.Diagnosis;

/// <summary>
/// Assembles a persistable <see cref="Core.Model.Diagnosis"/> row from the pipeline
/// outputs, carrying the full trace (detect / match / diagnose text) — Build Plan
/// Stage F step 3. The Gate fills <c>Route</c> / <c>GateReason</c> afterwards.
/// </summary>
public static class DiagnosisMapper
{
    public static Core.Model.Diagnosis ToEntity(
        Reading reading,
        ThresholdReport t1,
        HistoryReport t2,
        AnomalyVerdict a1,
        FaultVerdict a2,
        string traceId,
        DateTimeOffset at)
    {
        a2.Validate();

        return new Core.Model.Diagnosis
        {
            Id = Ids.Diagnosis(at),
            ReadingId = reading.Id,
            MachineId = reading.MachineId,
            Fault = a2.Fault,
            Severity = a2.Severity,
            Confidence = a2.Confidence,
            Status = DiagnosisStatus.Pending,
            DetectText = a1.Text,
            MatchText = t2.Trace,
            DiagnoseText = a2.Text,
            IncidentCites = string.Join(",", t2.Cites),
            TraceId = traceId,
            CreatedAt = at,
        };
    }
}
