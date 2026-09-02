using TireForge.Core.Abstractions;
using TireForge.Core.Acting;
using TireForge.Core.Agents;
using TireForge.Core.Gating;
using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Pipeline;

/// <summary>
/// The reliability pipeline (Build Plan Stage J): C→D→E→F→G→H→I for one reading,
/// under a single trace id. Pure orchestration over the Core ports — no cloud, no
/// Durable Functions (that wraps this later, Decision D2). A non-anomalous reading
/// stops after detection with no Diagnoses / WorkOrders rows.
/// </summary>
public sealed class Pipeline(
    IMachineStore machines,
    IReadingStore readings,
    IHistoryStore history,
    IDiagnosisStore diagnoses,
    IWorkOrderStore workOrders,
    IAnomalyDetector anomalyDetector,
    IFaultDiagnoser faultDiagnoser,
    IWorkOrderDrafter workOrderDrafter,
    TimeProvider? clock = null)
{
    private const int RecentWindow = 5;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<PipelineResult> RunAsync(Reading reading, CancellationToken ct = default)
    {
        var traceId = Ids.Trace();
        var at = _clock.GetUtcNow();
        var trace = new List<string>();
        void Log(string line) => trace.Add($"[{traceId}] {line}");

        var machine = await machines.GetAsync(reading.MachineId, ct)
            ?? throw new InvalidOperationException($"Unknown machine '{reading.MachineId}'.");

        // C — ThresholdCheck (T1)
        var t1 = ThresholdCheck.Evaluate(reading, machine);
        Log(t1.Trace);

        // D — Anomaly Detection (A1)
        var recent = await readings.RecentAsync(machine.Id, RecentWindow, ct);
        await readings.AddAsync(reading, ct);
        var a1 = await anomalyDetector.DetectAsync(reading, t1, recent, ct);
        a1.ApplyTo(reading);
        await readings.UpdateAsync(reading, ct);
        Log(a1.Text);

        if (!a1.IsAnomaly)
        {
            Log($"STOP {reading.Id}: not anomalous — no diagnosis");
            return new PipelineResult(traceId, reading.Id, machine.Id, t1.Severity, false, null, null, trace);
        }

        // E — HistoryMatch (T2)
        var t2 = await HistoryMatch.RunAsync(machine, t1, history, ct);
        Log(t2.Trace);

        // F — Fault Diagnosis (A2)
        var a2 = await faultDiagnoser.DiagnoseAsync(reading, t1, t2, a1, ct);
        var diagnosis = DiagnosisMapper.ToEntity(reading, t1, t2, a1, a2, traceId, at);
        Log(a2.Text);

        // G — the Gate
        var gate = Gate.Apply(diagnosis);
        Log($"GATE {diagnosis.Id}: route={gate.Route} — {gate.Reason}");
        await diagnoses.AddAsync(diagnosis, ct);

        // H — Work Order draft (A3)
        var draft = await workOrderDrafter.DraftAsync(diagnosis, machine, ct);

        // I — Act (sole write path)
        var writer = new WorkOrderWriter(workOrders, diagnoses);
        var act = await writer.ActAsync(diagnosis, draft, at, ct: ct);
        Log(act.WorkOrderIssued
            ? $"ACT {diagnosis.Id}: auto — {act.WorkOrder!.Id} issued"
            : $"ACT {diagnosis.Id}: review — pending, no work order");

        return new PipelineResult(traceId, reading.Id, machine.Id, t1.Severity, true, diagnosis, act, trace);
    }
}

/// <summary>Outcome of one <see cref="Pipeline.RunAsync"/>.</summary>
public sealed record PipelineResult(
    string TraceId,
    string ReadingId,
    string MachineId,
    Severity ThresholdSeverity,
    bool IsAnomaly,
    Diagnosis? Diagnosis,
    ActResult? Act,
    IReadOnlyList<string> Trace)
{
    public bool StoppedAtDetection => !IsAnomaly;
    public WorkOrder? WorkOrder => Act?.WorkOrder;
}
