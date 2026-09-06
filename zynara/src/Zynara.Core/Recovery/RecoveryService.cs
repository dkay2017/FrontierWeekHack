using Zynara.Core.Abstractions;
using Zynara.Core.Model;

namespace Zynara.Core.Recovery;

/// <summary>
/// Reads the denial-history cohorts from the store and returns the Estimated
/// Recoverable Value — the portfolio total and the per-scope lines that feed it.
/// </summary>
public sealed class RecoveryService(IZynaraStore store)
{
    public async Task<RecoveryReport> GetAsync(CancellationToken ct = default)
    {
        var cohorts = await store.GetDenialCohortsAsync(ct);

        var byScope = cohorts
            .Select(RecoveryEstimator.Estimate)
            .OrderByDescending(e => e.EstimatedRecoverableValue)
            .ToList();

        return new RecoveryReport(RecoveryEstimator.Portfolio(cohorts), byScope);
    }
}
