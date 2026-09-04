using Microsoft.EntityFrameworkCore;
using TireForge.Core.Abstractions;
using TireForge.Core.Model;

namespace TireForge.Data.Repositories;

/// <summary>EF Core implementations of the Core persistence ports (Stage A data-access module).</summary>
public sealed class MachineStore(TireForgeDbContext db) : IMachineStore
{
    public Task<Machine?> GetAsync(string machineId, CancellationToken ct = default) =>
        db.Machines.FirstOrDefaultAsync(m => m.Id == machineId, ct);

    public async Task<IReadOnlyList<Machine>> ListAsync(CancellationToken ct = default) =>
        await db.Machines.AsNoTracking().OrderBy(m => m.Id).ToListAsync(ct);
}

public sealed class ReadingStore(TireForgeDbContext db) : IReadingStore
{
    public Task<Reading?> GetAsync(string readingId, CancellationToken ct = default) =>
        db.Readings.FirstOrDefaultAsync(r => r.Id == readingId, ct);

    public async Task AddAsync(Reading reading, CancellationToken ct = default)
    {
        db.Readings.Add(reading);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Reading reading, CancellationToken ct = default)
    {
        db.Readings.Update(reading);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Reading>> RecentAsync(string machineId, int count, CancellationToken ct = default) =>
        await db.Readings.AsNoTracking()
            .Where(r => r.MachineId == machineId)
            .OrderByDescending(r => r.CapturedAt)
            .Take(count)
            .ToListAsync(ct);
}

public sealed class HistoryStore(TireForgeDbContext db) : IHistoryStore
{
    public async Task<IReadOnlyList<HistoryIncident>> MatchAsync(string machineId, string signature, CancellationToken ct = default) =>
        await db.History.AsNoTracking()
            .Where(h => h.MachineId == machineId && h.Signature == signature)
            .OrderByDescending(h => h.OccurredOn)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<HistoryIncident>> ForMachineAsync(string machineId, CancellationToken ct = default) =>
        await db.History.AsNoTracking()
            .Where(h => h.MachineId == machineId)
            .OrderByDescending(h => h.OccurredOn)
            .ToListAsync(ct);
}

public sealed class DiagnosisStore(TireForgeDbContext db) : IDiagnosisStore
{
    public Task<Diagnosis?> GetAsync(string diagnosisId, CancellationToken ct = default) =>
        db.Diagnoses.FirstOrDefaultAsync(d => d.Id == diagnosisId, ct);

    public async Task AddAsync(Diagnosis diagnosis, CancellationToken ct = default)
    {
        db.Diagnoses.Add(diagnosis);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Diagnosis diagnosis, CancellationToken ct = default)
    {
        db.Diagnoses.Update(diagnosis);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Diagnosis>> PendingAsync(CancellationToken ct = default) =>
        await db.Diagnoses.AsNoTracking()
            .Where(d => d.Status == DiagnosisStatus.Pending)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync(ct);
}

/// <summary>
/// The Work Order Adapter — the sole write path into <see cref="WorkOrder"/> (invariant 1.1).
/// </summary>
public sealed class WorkOrderStore(TireForgeDbContext db) : IWorkOrderStore
{
    public Task<WorkOrder?> GetAsync(string workOrderId, CancellationToken ct = default) =>
        db.WorkOrders.FirstOrDefaultAsync(w => w.Id == workOrderId, ct);

    public async Task AddAsync(WorkOrder workOrder, CancellationToken ct = default)
    {
        db.WorkOrders.Add(workOrder);
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WorkOrder workOrder, CancellationToken ct = default)
    {
        db.WorkOrders.Update(workOrder);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkOrder>> ListAsync(CancellationToken ct = default) =>
        await db.WorkOrders.AsNoTracking().OrderByDescending(w => w.CreatedAt).ToListAsync(ct);
}

/// <summary>T0 predictive early warnings (Core.Trends.TrendCheck) — advisory, separate from Diagnoses.</summary>
public sealed class EarlyWarningStore(TireForgeDbContext db) : IEarlyWarningStore
{
    public async Task AddAsync(EarlyWarning warning, CancellationToken ct = default)
    {
        db.EarlyWarnings.Add(warning);
        await db.SaveChangesAsync(ct);
    }

    public Task<EarlyWarning?> GetAsync(string id, CancellationToken ct = default) =>
        db.EarlyWarnings.FirstOrDefaultAsync(w => w.Id == id, ct);

    public async Task UpdateAsync(EarlyWarning warning, CancellationToken ct = default)
    {
        db.EarlyWarnings.Update(warning);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<EarlyWarning>> OpenAsync(CancellationToken ct = default) =>
        await db.EarlyWarnings.AsNoTracking()
            .Where(w => w.Status == EarlyWarningStatus.Open)
            .OrderByDescending(w => w.RaisedAt)
            .ToListAsync(ct);
}
