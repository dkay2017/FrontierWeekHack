using Zynara.Core.Abstractions;
using Zynara.Core.Model;
using Zynara.Core.Pipeline;

namespace Zynara.Core.View;

/// <summary>
/// Runs a request through the pipeline, projects it to a <see cref="CaseView"/>,
/// and stores the <see cref="CaseRecord"/> — the unit <c>Zynara.ApiProxy</c> calls.
/// Read paths and the reviewer decision go through here too, so the API functions
/// stay thin and the whole flow is unit-tested in Core.
/// </summary>
public sealed class CaseService(
    AuthPipeline pipeline,
    IZynaraStore store,
    ICaseRepository cases,
    TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<CaseRecord> RunAsync(Request request, CancellationToken ct = default)
    {
        var result = await pipeline.RunAsync(request, ct);
        var criteria = await store.GetCriteriaAsync(
            request.PayerPlan, request.Procedure, request.Region, ct);

        var view = CaseViewBuilder.Build(result, request, criteria);
        var record = new CaseRecord(request.Id, request, result, view, _clock.GetUtcNow());

        await cases.SaveAsync(record, ct);
        return record;
    }

    public Task<CaseRecord?> GetAsync(string requestId, CancellationToken ct = default) =>
        cases.GetAsync(requestId, ct);

    public Task<IReadOnlyList<CaseRecord>> ListAsync(CancellationToken ct = default) =>
        cases.ListAsync(ct);

    public async Task<CaseRecord?> RecordDecisionAsync(
        string requestId, string action, string? by, string? note, CancellationToken ct = default)
    {
        var record = await cases.GetAsync(requestId, ct);
        if (record is null) return null;

        var updated = record with
        {
            Decision = new ReviewerDecision(action, by, _clock.GetUtcNow(), note),
        };
        await cases.SaveAsync(updated, ct);
        return updated;
    }
}
