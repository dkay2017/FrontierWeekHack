using TireForge.Core.Model;

namespace TireForge.Core.Abstractions;

/// <summary>
/// Read-only aggregate queries for the dashboard read models (Build Plan Stage L).
/// Kept separate from the write-path stores so those stay lean.
/// </summary>
public interface IReportingQueries
{
    /// <summary>The most recent reading for each machine that has one.</summary>
    Task<IReadOnlyDictionary<string, Reading>> LatestReadingPerMachineAsync(CancellationToken ct = default);

    /// <summary>Count of anomalous readings per machine since <paramref name="since"/>.</summary>
    Task<IReadOnlyDictionary<string, int>> AnomalyCountsSinceAsync(DateTimeOffset since, CancellationToken ct = default);

    Task<int> ReadingCountAsync(CancellationToken ct = default);

    Task<int> DiagnosisCountAsync(CancellationToken ct = default);
}
