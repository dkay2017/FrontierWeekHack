using Microsoft.EntityFrameworkCore;
using TireForge.Core.Abstractions;
using TireForge.Core.Model;

namespace TireForge.Data.Reporting;

/// <summary>EF Core implementation of the dashboard aggregate queries (Build Plan Stage L).</summary>
public sealed class ReportingQueries(TireForgeDbContext db) : IReportingQueries
{
    public async Task<IReadOnlyDictionary<string, Reading>> LatestReadingPerMachineAsync(CancellationToken ct = default)
    {
        var readings = await db.Readings.AsNoTracking().ToListAsync(ct);
        return readings
            .GroupBy(r => r.MachineId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(r => r.CapturedAt).First());
    }

    public async Task<IReadOnlyDictionary<string, int>> AnomalyCountsSinceAsync(
        DateTimeOffset since, CancellationToken ct = default)
    {
        var rows = await db.Readings.AsNoTracking()
            .Where(r => r.IsAnomaly == true && r.CapturedAt >= since)
            .Select(r => r.MachineId)
            .ToListAsync(ct);
        return rows.GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count());
    }

    public Task<int> ReadingCountAsync(CancellationToken ct = default) =>
        db.Readings.AsNoTracking().CountAsync(ct);

    public Task<int> DiagnosisCountAsync(CancellationToken ct = default) =>
        db.Diagnoses.AsNoTracking().CountAsync(ct);

    public async Task<IReadOnlyList<AgentCallTotals>> AgentCallTotalsAsync(CancellationToken ct = default)
    {
        var rows = await db.AgentCalls.AsNoTracking()
            .GroupBy(a => new { a.AgentName, a.Model })
            .Select(g => new AgentCallTotals(
                g.Key.AgentName,
                g.Key.Model,
                g.Count(),
                g.Sum(a => (long)a.PromptTokens),
                g.Sum(a => (long)a.CompletionTokens)))
            .ToListAsync(ct);
        return rows;
    }
}
