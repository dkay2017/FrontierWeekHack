using Zynara.Core.Model;

namespace Zynara.Core.Abstractions;

/// <summary>
/// The read/write seam over operational state (Cosmos DB) and the corpus metadata.
/// The pure pipeline depends only on this; <c>Zynara.Data</c> supplies the Cosmos
/// implementation and <see cref="InMemoryZynaraStore"/> backs the tests.
/// </summary>
public interface IZynaraStore
{
    // --- reads the spokes need ------------------------------------------------

    /// <summary>Matching payer rule-set entries for a procedure in a region (0, 1, or several — see NeedsAuthCheck).</summary>
    Task<IReadOnlyList<PolicyRule>> GetPolicyRulesAsync(
        string payerPlan, Region region, string procedure, CancellationToken ct = default);

    /// <summary>The written approval criteria for a procedure at the current policy version.</summary>
    Task<Criteria?> GetCriteriaAsync(
        string payerPlan, string procedure, Region region, CancellationToken ct = default);

    /// <summary>Precedent metadata for the payer + procedure + region (AppealMatch filters and ranks these).</summary>
    Task<IReadOnlyList<Precedent>> GetPrecedentsAsync(
        string payerPlan, string procedure, Region region, CancellationToken ct = default);

    /// <summary>The approved authorisation for a request, if one exists (ExpiryMath).</summary>
    Task<AuthRecord?> GetAuthAsync(string requestId, CancellationToken ct = default);

    /// <summary>All stored versions of a policy document, oldest first (PolicyDiff).</summary>
    Task<IReadOnlyList<PolicyVersion>> GetPolicyVersionsAsync(string policyRef, CancellationToken ct = default);

    // --- writes ------------------------------------------------------------------

    Task SaveEarlyWarningAsync(EarlyWarning warning, CancellationToken ct = default);
}
