using OpenAI.Responses;

namespace Zynara.Agents.Foundry;

/// <summary>
/// Creates the five persistent Foundry agents ("create once, reuse forever" —
/// Challenge 1 &amp; 4). Run once per environment from the provisioner console, or
/// on host startup via <see cref="Zynara.Agents.DependencyInjection.EnsureZynaraAgentsAsync"/>.
/// File Search is attached only when a vector store id is configured.
/// </summary>
public sealed class FoundryAgentProvisioner(FoundryAgentClient client, FoundryAgentOptions options)
{
    public sealed record AgentSpec(string Name, string Instructions, IReadOnlyList<ResponseTool>? Tools);

    public IReadOnlyList<AgentSpec> Specs
    {
        get
        {
            var fileSearch = options.VectorStoreId is { Length: > 0 } vsId
                ? new ResponseTool[] { ResponseTool.CreateFileSearchTool([vsId]) }
                : null;

            return
            [
                new(options.NeedsAuthAgentName, AgentPrompts.NeedsAuth, fileSearch),
                new(options.EvidenceGapAgentName, AgentPrompts.EvidenceGap, fileSearch),
                new(options.AppealBuilderAgentName, AgentPrompts.AppealBuilder, fileSearch),
                new(options.ExpiryWatchAgentName, AgentPrompts.ExpiryWatch, null),
                new(options.PolicyDriftAgentName, AgentPrompts.PolicyDrift, null),
            ];
        }
    }

    /// <summary>Ensure every agent exists (no-op for any that already does).</summary>
    public async Task EnsureAllAsync(CancellationToken ct = default)
    {
        foreach (var s in Specs)
            await client.EnsureAgentAsync(s.Name, s.Instructions, s.Tools, ct);
    }

    /// <summary>Push a fresh version of every agent (prompt / tool changes). Returns the new version ids.</summary>
    public async Task<IReadOnlyList<string>> CreateVersionsAsync(CancellationToken ct = default)
    {
        var ids = new List<string>();
        foreach (var s in Specs)
            ids.Add($"{s.Name} -> {await client.CreateVersionAsync(s.Name, s.Instructions, s.Tools, ct)}");
        return ids;
    }
}
