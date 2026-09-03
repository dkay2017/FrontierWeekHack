using OpenAI.Responses;

namespace TireForge.Agents.Foundry;

/// <summary>
/// Creates the three persistent Foundry agents (Stage M / Challenge 1 & 4:
/// "create once, reuse forever"). Pure C# — the D11 rung-0 path. Run once per
/// environment from the provisioner console, or on host startup.
/// </summary>
public sealed class FoundryAgentProvisioner(FoundryAgentClient client, FoundryAgentOptions options)
{
    public IReadOnlyList<AgentSpec> Specs =>
    [
        new(options.AnomalyAgentName, AgentPrompts.AnomalyDetection, [ThresholdsTool.Definition()]),
        new(options.FaultAgentName, AgentPrompts.FaultDiagnosis, null),
        new(options.WorkOrderAgentName, AgentPrompts.WorkOrder, null),
    ];

    /// <summary>Ensure all three agents exist (no-op for any that already do).</summary>
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

    public sealed record AgentSpec(string Name, string Instructions, IReadOnlyList<ResponseTool>? Tools);
}
