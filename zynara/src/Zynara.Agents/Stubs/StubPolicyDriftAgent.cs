using Zynara.Core.Agents;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>policy-drift</c> twin. Explains the operational impact of a
/// criteria change that <c>PolicyDiff</c> already computed. Advisory only.
/// </summary>
public sealed class StubPolicyDriftAgent : IPolicyDriftAgent
{
    public Task<string> ExplainImpactAsync(
        string policyRef,
        IReadOnlyList<string> addedCriteria,
        IReadOnlyList<string> removedCriteria,
        IReadOnlyList<string> affectedTemplates,
        CancellationToken ct = default)
    {
        if (addedCriteria.Count == 0 && removedCriteria.Count == 0)
            return Task.FromResult($"No material change to {policyRef}.");

        var parts = new List<string> { $"{policyRef} changed." };

        if (addedCriteria.Count > 0)
            parts.Add(
                $"{addedCriteria.Count} new requirement(s): {string.Join("; ", addedCriteria)}. " +
                "Submissions that were complete under the old version may now show a gap.");

        if (removedCriteria.Count > 0)
            parts.Add($"{removedCriteria.Count} requirement(s) dropped: {string.Join("; ", removedCriteria)}.");

        parts.Add(affectedTemplates.Count > 0
            ? $"{affectedTemplates.Count} request template(s) need review: {string.Join(", ", affectedTemplates)}."
            : "No stored templates are affected.");

        return Task.FromResult(string.Join(" ", parts));
    }
}
