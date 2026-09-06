using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 5 (runs continuously, advisory). Diffs the two most recent versions of a
/// payer policy document — added and removed criteria — and, when there is a
/// material change, has the <c>policy-drift</c> agent explain the operational
/// impact and raises an <see cref="EarlyWarning"/>.
/// </summary>
public sealed class PolicyDiff(IZynaraStore store, IPolicyDriftAgent agent)
{
    public async Task<DriftResult> RunAsync(
        string policyRef, IReadOnlyList<string> affectedTemplates, CancellationToken ct = default)
    {
        var versions = (await store.GetPolicyVersionsAsync(policyRef, ct))
            .OrderBy(v => v.EffectiveFrom)
            .ToList();

        if (versions.Count < 2)
            return new DriftResult(Array.Empty<string>(), Array.Empty<string>(), null);

        var prev = versions[^2];
        var curr = versions[^1];

        var prevTexts = prev.Criteria.Select(c => c.Text.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var currTexts = curr.Criteria.Select(c => c.Text.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var added = currTexts.Except(prevTexts).ToList();
        var removed = prevTexts.Except(currTexts).ToList();

        if (added.Count == 0 && removed.Count == 0)
            return new DriftResult(added, removed, null);

        var text = await agent.ExplainImpactAsync(policyRef, added, removed, affectedTemplates, ct);
        var warning = new EarlyWarning(
            Id: $"ew-drift-{policyRef}-{curr.Version}",
            Kind: "drift",
            SubjectRef: policyRef,
            Text: text,
            RaisedOn: DateOnly.FromDateTime(DateTime.UtcNow));

        await store.SaveEarlyWarningAsync(warning, ct);
        return new DriftResult(added, removed, warning);
    }
}
