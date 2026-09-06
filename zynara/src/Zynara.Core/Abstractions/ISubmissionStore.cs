using Zynara.Core.Model;

namespace Zynara.Core.Abstractions;

/// <summary>
/// Read/write seam over outbound submissions. <c>Zynara.Submission</c> writes;
/// the dashboard reads the outcome back. <see cref="InMemorySubmissionStore"/>
/// backs local runs and the demo; <c>Zynara.Data</c> supplies the Cosmos
/// implementation (<c>submissions</c>, partition <c>/requestId</c>).
/// </summary>
public interface ISubmissionStore
{
    Task<SubmissionRecord?> GetAsync(string requestId, CancellationToken ct = default);

    Task<IReadOnlyList<SubmissionRecord>> ListAsync(CancellationToken ct = default);

    Task SaveAsync(SubmissionRecord record, CancellationToken ct = default);
}

/// <summary>In-memory <see cref="ISubmissionStore"/>, newest-first on list. Thread-safe for the API host.</summary>
public sealed class InMemorySubmissionStore : ISubmissionStore
{
    private readonly Dictionary<string, SubmissionRecord> _records = new();
    private readonly object _gate = new();

    public Task<SubmissionRecord?> GetAsync(string requestId, CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult(_records.GetValueOrDefault(requestId));
    }

    public Task<IReadOnlyList<SubmissionRecord>> ListAsync(CancellationToken ct = default)
    {
        lock (_gate)
            return Task.FromResult<IReadOnlyList<SubmissionRecord>>(
                _records.Values.OrderByDescending(r => r.SubmittedAt).ToList());
    }

    public Task SaveAsync(SubmissionRecord record, CancellationToken ct = default)
    {
        lock (_gate)
            _records[record.RequestId] = record;
        return Task.CompletedTask;
    }
}
