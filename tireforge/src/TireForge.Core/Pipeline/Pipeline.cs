using System.Diagnostics;
using TireForge.Core.Abstractions;
using TireForge.Core.Acting;
using TireForge.Core.Agents;
using TireForge.Core.Gating;
using TireForge.Core.History;
using TireForge.Core.Model;
using TireForge.Core.Observability;
using TireForge.Core.Thresholds;
using TireForge.Core.Trends;

namespace TireForge.Core.Pipeline;

/// <summary>
/// The reliability pipeline (Build Plan Stage J): C→D→E→F→G→H→I for one reading,
/// under a single trace id. Pure orchestration over the Core ports — no cloud, no
/// Durable Functions (that wraps this later, Decision D2). A non-anomalous reading
/// stops after detection with no Diagnoses / WorkOrders rows.
///
/// Every step runs inside a child <see cref="Activity"/> of one root span
/// (Decision D6), so App Insights shows the whole flow under one correlated id.
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
    IEarlyWarningStore earlyWarningStore,
    TimeProvider? clock = null)
{
    private const int RecentWindow = 5;
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<PipelineResult> RunAsync(Reading reading, CancellationToken ct = default)
    {
        using var root = Telemetry.Source.StartActivity(Telemetry.Spans.Run, ActivityKind.Internal);
        root?.SetTag(Telemetry.Tags.ReadingId, reading.Id);
        root?.SetTag(Telemetry.Tags.MachineId, reading.MachineId);

        var at = _clock.GetUtcNow();
        var traceId = root?.TraceId.ToString() ?? Ids.Trace();
        var trace = new List<string>();
        void Log(string line) => trace.Add($"[{traceId}] {line}");

        var machine = await machines.GetAsync(reading.MachineId, ct)
            ?? throw Fail(root, new InvalidOperationException($"Unknown machine '{reading.MachineId}'."));

        // C — ThresholdCheck (T1)
        ThresholdReport t1;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.ThresholdCheck))
        {
            t1 = ThresholdCheck.Evaluate(reading, machine);
            s?.SetTag(Telemetry.Tags.Severity, t1.Severity.ToString());
        }
        Log(t1.Trace);

        // D — Anomaly Detection (A1)
        var recent = await readings.RecentAsync(machine.Id, RecentWindow, ct);
        AnomalyVerdict a1;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.Detect))
        {
            await readings.AddAsync(reading, ct);
            a1 = await anomalyDetector.DetectAsync(reading, t1, recent, ct);
            a1.ApplyTo(reading);
            await readings.UpdateAsync(reading, ct);
            s?.SetTag(Telemetry.Tags.Anomaly, a1.IsAnomaly);
        }
        Log(a1.Text);

        // T0 — TrendCheck: predictive early warning. Runs regardless of A1's verdict —
        // it's a different signal (trajectory, not current state) — using the same
        // recent-reading window already fetched above. Advisory only: never gates
        // the Diagnosis/WorkOrder path below, and never stops the pipeline.
        var raisedWarnings = new List<EarlyWarning>();
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.TrendCheck))
        {
            var t0 = TrendCheck.Evaluate(reading, recent, machine, t1);
            foreach (var warning in t0.Warnings)
            {
                var entity = EarlyWarningMapper.ToEntity(warning, reading, traceId, at);
                await earlyWarningStore.AddAsync(entity, ct);
                raisedWarnings.Add(entity);
            }
            s?.SetTag(Telemetry.Tags.EarlyWarningCount, raisedWarnings.Count);
            Log(t0.Trace);
        }

        if (!a1.IsAnomaly)
        {
            Log($"STOP {reading.Id}: not anomalous — no diagnosis");
            root?.SetTag(Telemetry.Tags.Anomaly, false);
            return new PipelineResult(traceId, reading.Id, machine.Id, t1.Severity, false, null, null, trace, raisedWarnings);
        }

        // E — HistoryMatch (T2)
        HistoryReport t2;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.HistoryMatch))
        {
            t2 = await HistoryMatch.RunAsync(machine, t1, history, ct);
            s?.SetTag("tireforge.history_matches", t2.Incidents.Count);
        }
        Log(t2.Trace);

        // F — Fault Diagnosis (A2)
        Diagnosis diagnosis;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.Diagnose))
        {
            var a2 = await faultDiagnoser.DiagnoseAsync(reading, t1, t2, a1, ct);
            diagnosis = DiagnosisMapper.ToEntity(reading, t1, t2, a1, a2, traceId, at);
            s?.SetTag(Telemetry.Tags.DiagnosisId, diagnosis.Id);
            s?.SetTag(Telemetry.Tags.Severity, diagnosis.Severity.ToString());
            s?.SetTag(Telemetry.Tags.Confidence, diagnosis.Confidence);
            Log(a2.Text);
        }

        // G — the Gate
        GateDecision gate;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.Gate))
        {
            gate = Gate.Apply(diagnosis);
            s?.SetTag(Telemetry.Tags.GateRoute, gate.Route.ToString());
        }
        Log($"GATE {diagnosis.Id}: route={gate.Route} — {gate.Reason}");
        await diagnoses.AddAsync(diagnosis, ct);

        // H — Work Order draft (A3)
        WorkOrderDraft draft;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.Draft))
        {
            draft = await workOrderDrafter.DraftAsync(diagnosis, machine, ct);
        }

        // I — Act (sole write path)
        ActResult act;
        using (var s = Telemetry.Source.StartActivity(Telemetry.Spans.Act))
        {
            act = await new WorkOrderWriter(workOrders, diagnoses).ActAsync(diagnosis, draft, at, ct: ct);
            s?.SetTag(Telemetry.Tags.GateRoute, act.Route.ToString());
            if (act.WorkOrder is not null)
                s?.SetTag(Telemetry.Tags.WorkOrderId, act.WorkOrder.Id);
        }
        Log(act.WorkOrderIssued
            ? $"ACT {diagnosis.Id}: auto — {act.WorkOrder!.Id} issued"
            : $"ACT {diagnosis.Id}: review — pending, no work order");

        root?.SetTag(Telemetry.Tags.DiagnosisId, diagnosis.Id);
        root?.SetTag(Telemetry.Tags.GateRoute, gate.Route.ToString());
        return new PipelineResult(traceId, reading.Id, machine.Id, t1.Severity, true, diagnosis, act, trace, raisedWarnings);
    }

    private static Exception Fail(Activity? activity, Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        return ex;
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
    IReadOnlyList<string> Trace,
    IReadOnlyList<EarlyWarning> EarlyWarnings)
{
    public bool StoppedAtDetection => !IsAnomaly;
    public WorkOrder? WorkOrder => Act?.WorkOrder;
}
