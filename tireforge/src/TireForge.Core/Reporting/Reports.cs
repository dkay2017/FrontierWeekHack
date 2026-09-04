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
    IEarlyWarningStore earlyWarnings,
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

    /// <summary>T0 predictive early warnings (Decision D17) — open ones, newest first.</summary>
    public async Task<WarningsResponse> WarningsAsync(CancellationToken ct = default)
    {
        var names = await MachineNames(ct);
        var open = await earlyWarnings.OpenAsync(ct);
        var items = open.Select(w => new EarlyWarningView(
            w.Id, w.MachineId, names.GetValueOrDefault(w.MachineId, w.MachineId),
            w.Sensor, w.CurrentValue, w.Unit, w.RateOfChangePerHour, w.BoundApproached,
            w.ProjectedBreachAt, w.HoursToBreachAt, w.Confidence, w.NarrativeText,
            w.Status, w.RaisedAt, w.TraceId)).ToList();

        return new WarningsResponse(items, _clock.GetUtcNow());
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

    // gpt-5.4 pricing estimate (USD per 1M tokens) — spend is real tokens × this.
    private const decimal PriceInPer1M = 2.50m;
    private const decimal PriceOutPer1M = 10.00m;

    private static readonly Dictionary<string, string> AgentDisplayNames = new()
    {
        ["anomaly-detection-agent"] = "Anomaly Detection",
        ["fault-diagnosis-agent"] = "Fault Diagnosis",
        ["work-order-agent"] = "Work Order",
    };

    public async Task<CostResponse> CostAsync(CancellationToken ct = default)
    {
        var metered = await queries.AgentCallTotalsAsync(ct);

        if (metered.Any(m => m.TotalTokens > 0))
        {
            var agents = metered
                .OrderBy(m => m.AgentName)
                .Select(m =>
                {
                    var spend = m.PromptTokens / 1_000_000m * PriceInPer1M
                              + m.CompletionTokens / 1_000_000m * PriceOutPer1M;
                    return new AgentCostView(
                        AgentDisplayNames.GetValueOrDefault(m.AgentName, m.AgentName),
                        m.Model, m.Calls, m.TotalTokens, Math.Round(spend, 4));
                })
                .ToList();

            return new CostResponse(agents, TokenMetricsAvailable: true,
                Note: $"Tokens are real (from each agent response). Spend is estimated at " +
                      $"${PriceInPer1M}/1M input + ${PriceOutPer1M}/1M output for gpt-5.4.",
                GeneratedAt: _clock.GetUtcNow());
        }

        // No metered calls yet (running with stubs, or no anomalous readings).
        var readingCalls = await queries.ReadingCountAsync(ct);
        var diagnosisCalls = await queries.DiagnosisCountAsync(ct);
        var placeholder = new List<AgentCostView>
        {
            new("Anomaly Detection", "gpt-5.4", readingCalls, null, null),
            new("Fault Diagnosis", "gpt-5.4", diagnosisCalls, null, null),
            new("Work Order", "gpt-5.4", diagnosisCalls, null, null),
        };
        return new CostResponse(placeholder, TokenMetricsAvailable: false,
            Note: "Call counts are from the pipeline's own records. Token and spend figures " +
                  "populate once the real Foundry agents run (TIREFORGE_AGENTS=foundry).",
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
