using Zynara.Core.Agents;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>policy-drift</c> backed by the hosted agent — explains the impact of a
/// criteria change that <c>PolicyDiff</c> already computed.
/// </summary>
public sealed class FoundryPolicyDriftAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IPolicyDriftAgent
{
    public async Task<string> ExplainImpactAsync(
        string policyRef,
        IReadOnlyList<string> addedCriteria,
        IReadOnlyList<string> removedCriteria,
        IReadOnlyList<string> affectedTemplates,
        CancellationToken ct = default)
    {
        var prompt =
            $"Policy {policyRef} changed.\n" +
            $"Added criteria: {(addedCriteria.Count > 0 ? string.Join("; ", addedCriteria) : "none")}\n" +
            $"Removed criteria: {(removedCriteria.Count > 0 ? string.Join("; ", removedCriteria) : "none")}\n" +
            $"Request templates referencing this policy: " +
            $"{(affectedTemplates.Count > 0 ? string.Join(", ", affectedTemplates) : "none")}";

        var inv = await client.InvokeAsync(options.PolicyDriftAgentName, prompt, toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.PolicyDriftAgentName, options.Model, inv, requestId: null, ct);
        return inv.Text.Trim();
    }
}
