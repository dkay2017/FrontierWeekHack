using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Core.Reporting;

// Read-model response shapes for TireForge.ApiProxy (Build Plan Stage L).
// The dashboard's mock `api` object maps 1:1 onto these.

// --- /status -------------------------------------------------------------
public sealed record SensorReadout(double Value, double Min, double Max, string Unit, SensorStatus Standing);

public sealed record MachineStatusView(
    string Id,
    string Name,
    string Role,
    DateOnly? LastMaintenance,
    SensorReadout Temperature,
    SensorReadout Pressure,
    SensorReadout Vibration,
    SensorReadout Rpm,
    Severity Status,
    int Anomalies24h);

public sealed record StatusResponse(IReadOnlyList<MachineStatusView> Machines, DateTimeOffset GeneratedAt);

// --- /queue -----------------------------------------------------------
public sealed record QueueItemView(
    string Id,
    string MachineId,
    string MachineName,
    string Fault,
    Severity Severity,
    double Confidence,
    DateTimeOffset RaisedAt,
    string ReadingId,
    GateRoute Route,
    string GateReason,
    IReadOnlyList<string> IncidentCites,
    string DetectText,
    string MatchText,
    string DiagnoseText,
    string DraftActionText,
    string TraceId);

public sealed record QueueResponse(IReadOnlyList<QueueItemView> Items, DateTimeOffset GeneratedAt);

// --- /workorders ----------------------------------------------------
public sealed record WorkOrderView(
    string Id,
    string DiagnosisId,
    string MachineId,
    string MachineName,
    string Fault,
    Severity Severity,
    string ReadingId,
    DateTimeOffset RaisedAt,
    WorkOrderStatus Status,
    string By,
    string? RejectNote,
    DateTimeOffset? ClosedAt);

public sealed record WorkOrdersResponse(IReadOnlyList<WorkOrderView> Items, DateTimeOffset GeneratedAt);

// --- /health ------------------------------------------------------
public sealed record HealthResponse(
    int MachinesInSpec,
    int MachineCount,
    int Anomalies24h,
    IReadOnlyDictionary<string, int> AnomaliesByMachine24h,
    int WorkOrdersOpen,
    int WorkOrdersClosed,
    double ResolutionRate,
    DateTimeOffset GeneratedAt);

// --- /cost -------------------------------------------------------
// Call counts come from our own tables; token / spend figures stay null until the
// real Foundry agents emit gen_ai.* spans (Decision D8, invariant 1.5 — never
// present mocked figures as real).
public sealed record AgentCostView(string Agent, string Model, int Calls, long? Tokens, decimal? Spend);

public sealed record CostResponse(
    IReadOnlyList<AgentCostView> Agents,
    bool TokenMetricsAvailable,
    string Note,
    DateTimeOffset GeneratedAt);

// --- /warnings ------------------------------------------------------
// T0 predictive early warnings (Core.Trends.TrendCheck, Decision D17) — a sensor
// still in spec today but trending toward a breach. Advisory, separate from the
// reactive /queue (which is T1/A2-driven, post-breach).
public sealed record EarlyWarningView(
    string Id,
    string MachineId,
    string MachineName,
    SensorKind Sensor,
    double CurrentValue,
    string Unit,
    double RateOfChangePerHour,
    double BoundApproached,
    DateTimeOffset ProjectedBreachAt,
    double HoursToBreachAt,
    double Confidence,
    string NarrativeText,
    EarlyWarningStatus Status,
    DateTimeOffset RaisedAt,
    string TraceId);

public sealed record WarningsResponse(IReadOnlyList<EarlyWarningView> Items, DateTimeOffset GeneratedAt);
