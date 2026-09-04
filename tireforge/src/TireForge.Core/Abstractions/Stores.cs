using TireForge.Core.Model;

namespace TireForge.Core.Abstractions;

/// <summary>
/// Persistence ports used by the pipeline (Build Plan Stage A "data-access module").
/// Implemented in <c>TireForge.Data</c> over EF Core / SQLite. The pure Core layer
/// depends only on these interfaces.
/// </summary>
public interface IMachineStore
{
    Task<Machine?> GetAsync(string machineId, CancellationToken ct = default);
    Task<IReadOnlyList<Machine>> ListAsync(CancellationToken ct = default);
}

public interface IReadingStore
{
    Task<Reading?> GetAsync(string readingId, CancellationToken ct = default);
    Task AddAsync(Reading reading, CancellationToken ct = default);

    /// <summary>Persist a change to an existing reading (e.g. <c>IsAnomaly</c>).</summary>
    Task UpdateAsync(Reading reading, CancellationToken ct = default);

    /// <summary>Most recent readings for a machine, newest first.</summary>
    Task<IReadOnlyList<Reading>> RecentAsync(string machineId, int count, CancellationToken ct = default);
}

public interface IHistoryStore
{
    /// <summary>Past incidents for a machine whose signature matches <paramref name="signature"/>.</summary>
    Task<IReadOnlyList<HistoryIncident>> MatchAsync(string machineId, string signature, CancellationToken ct = default);

    Task<IReadOnlyList<HistoryIncident>> ForMachineAsync(string machineId, CancellationToken ct = default);
}

public interface IDiagnosisStore
{
    Task<Diagnosis?> GetAsync(string diagnosisId, CancellationToken ct = default);
    Task AddAsync(Diagnosis diagnosis, CancellationToken ct = default);
    Task UpdateAsync(Diagnosis diagnosis, CancellationToken ct = default);

    /// <summary>Diagnoses awaiting a reviewer decision.</summary>
    Task<IReadOnlyList<Diagnosis>> PendingAsync(CancellationToken ct = default);
}

/// <summary>
/// The Work Order Adapter — the sole write path into <see cref="WorkOrder"/>
/// (invariant 1.1). No other code inserts work-order rows.
/// </summary>
public interface IWorkOrderStore
{
    Task<WorkOrder?> GetAsync(string workOrderId, CancellationToken ct = default);
    Task AddAsync(WorkOrder workOrder, CancellationToken ct = default);
    Task UpdateAsync(WorkOrder workOrder, CancellationToken ct = default);
    Task<IReadOnlyList<WorkOrder>> ListAsync(CancellationToken ct = default);
}

/// <summary>T0 predictive early warnings (Core.Trends.TrendCheck) — advisory, separate from Diagnoses.</summary>
public interface IEarlyWarningStore
{
    Task AddAsync(EarlyWarning warning, CancellationToken ct = default);
    Task<EarlyWarning?> GetAsync(string id, CancellationToken ct = default);
    Task UpdateAsync(EarlyWarning warning, CancellationToken ct = default);

    /// <summary>Open warnings, newest first.</summary>
    Task<IReadOnlyList<EarlyWarning>> OpenAsync(CancellationToken ct = default);
}
