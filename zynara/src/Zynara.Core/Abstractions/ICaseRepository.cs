using Zynara.Core.Model;

namespace Zynara.Core.Abstractions;

/// <summary>
/// Read/write seam over assembled cases. <c>Zynara.ApiProxy</c> reads these for the
/// reviewer dashboard; the orchestrator (or the API's own run endpoint) writes
/// them. <see cref="InMemoryCaseRepository"/> backs local runs and the demo;
/// <c>Zynara.Data</c> supplies the Cosmos implementation.
/// </summary>
public interface ICaseRepository
{
    Task<CaseRecord?> GetAsync(string requestId, CancellationToken ct = default);

    Task<IReadOnlyList<CaseRecord>> ListAsync(CancellationToken ct = default);

    Task SaveAsync(CaseRecord record, CancellationToken ct = default);
}

/// <summary>In-memory <see cref="ICaseRepository"/>, newest-first on list. Thread-safe for the API host.</summary>
public sealed class InMemoryCaseRepository : ICaseRepository
{
    private readonly Dictionary<string, CaseRecord> _cases = new();
    private readonly object _gate = new();

    public Task<CaseRecord?> GetAsync(string requestId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_cases.GetValueOrDefault(requestId));
    }

    public Task<IReadOnlyList<CaseRecord>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<CaseRecord>>(
                _cases.Values.OrderByDescending(c => c.RunAt).ToList());
    }

    public Task SaveAsync(CaseRecord record, CancellationToken ct = default)
    {
        lock (_gate)
            _cases[record.RequestId] = record;
        return Task.CompletedTask;
    }
}
