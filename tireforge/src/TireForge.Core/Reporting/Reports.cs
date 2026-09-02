using TireForge.Core.Abstractions;
using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Reporting;

/// <summary>
/// Pure read models for the dashboard (Build Plan Stage L) — machine status,
/// the review queue, the work-order log, health metrics, and cost/call counts.
/// No writes.
/// </summary>
public sealed class Reports(
    IMachineStore machines,
    IDiagnosisStore diagnoses,
    IWorkOrderStore workOrders,
    IReportingQueries queries,
    TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<StatusResponse> StatusAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var since24h = now.AddHours(-24);
        var all = await machines.ListAsync(ct);
        var latest = await queries.LatestReadingPerMachineAsync(ct);
        var anomalies = await queries.AnomalyCountsSinceAsync(since24h, ct);

        var views = all.Select(m =>
        {
            latest.TryGetValue(m.Id, out var reading);
            var t1 = reading is null ? null : (ThresholdReport?)ThresholdCheck.Evaluate(reading, m);
            return new MachineStatusView(
                m.Id, m.Name, m.Description, m.LastMaintenance,
                Readout(m, SensorKind.Temperature, reading, t1),
                Readout(m, SensorKind.Pressure, reading, t1),
                Readout(m, SensorKind.Vibration, reading, t1),
                Readout(m, SensorKind.Rpm, reading, t1),
                t1?.Severity ?? Severity.Info,
                anomalies.GetValueOrDefault(m.Id));
        }).ToList();

        return new StatusResponse(views, now);
    }

    public async Task<QueueResponse> QueueAsync(CancellationToken ct = default)
    {
        var names = await MachineNames(ct);
        var pending = await diagnoses.PendingAsync(ct);
        var items = pending.Select(d => new QueueItemView(
            d.Id, d.MachineId, names.GetValueOrDefault(d.MachineId, d.MachineId),
            d.Fault, d.Severity, d.Confidence, d.CreatedAt, d.ReadingId, d.Route, d.GateReason,
            SplitCites(d.IncidentCites),
            d.DetectText, d.MatchText, d.DiagnoseText, d.DraftActionText, d.TraceId)).ToList();

        return new QueueResponse(items, _clock.GetUtcNow());
    }

    public async Task<WorkOrdersResponse> WorkOrdersAsync(CancellationToken ct = default)
    {
        var names = await MachineNames(ct);
        var all = await workOrders.ListAsync(ct);
        var items = all.Select(w => new WorkOrderView(
            w.Id, w.DiagnosisId, w.MachineId, names.GetValueOrDefault(w.MachineId, w.MachineId),
            w.Fault, w.Severity, w.ReadingId, w.CreatedAt, w.Status,
            w.IssuedBy == "system" ? "auto" : "reviewer",
            w.RejectNote, w.ClosedAt)).ToList();

        return new WorkOrdersResponse(items, _clock.GetUtcNow());
    }

    public async Task<HealthResponse> HealthAsync(CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var status = await StatusAsync(ct);
        var wos = await workOrders.ListAsync(ct);

        var inSpec = status.Machines.Count(m => m.Status == Severity.Info);
        var open = wos.Count(w => w.Status is WorkOrderStatus.Issued or WorkOrderStatus.Approved);
        var closed = wos.Count(w => w.Status == WorkOrderStatus.Closed);
        var decided = wos.Count(w => w.Status != WorkOrderStatus.Rejected);
        var resolution = decided == 0 ? 0 : Math.Round((double)closed / decided, 2);
        var byMachine = status.Machines.ToDictionary(m => m.Id, m => m.Anomalies24h);

        return new HealthResponse(
            inSpec, status.Machines.Count,
            byMachine.Values.Sum(), byMachine,
            open, closed, resolution, now);
    }

    public async Task<CostResponse> CostAsync(CancellationToken ct = default)
    {
        var readingCalls = await queries.ReadingCountAsync(ct);
        var diagnosisCalls = await queries.DiagnosisCountAsync(ct);

        var agents = new List<AgentCostView>
        {
            new("Anomaly Detection", "gpt-5.4", readingCalls, null, null),
            new("Fault Diagnosis", "gpt-5.4", diagnosisCalls, null, null),
            new("Work Order", "gpt-5.4", diagnosisCalls, null, null),
        };

        return new CostResponse(agents, TokenMetricsAvailable: false,
            Note: "Call counts are from the pipeline's own records. Token and spend figures " +
                  "populate once the real Foundry agents run and emit gen_ai.* spans (Stage M).",
            GeneratedAt: _clock.GetUtcNow());
    }

    private async Task<Dictionary<string, string>> MachineNames(CancellationToken ct) =>
        (await machines.ListAsync(ct)).ToDictionary(m => m.Id, m => m.Name);

    private static SensorReadout Readout(Machine m, SensorKind kind, Reading? reading, ThresholdReport? t1)
    {
        var band = m.Band(kind);
        var value = reading?.Value(kind) ?? double.NaN;
        var standing = t1?.Sensors.FirstOrDefault(s => s.Sensor == kind)?.Status ?? SensorStatus.Ok;
        return new SensorReadout(value, band.Min, band.Max, band.Unit, standing);
    }

    private static IReadOnlyList<string> SplitCites(string csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
