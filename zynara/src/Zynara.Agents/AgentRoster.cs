using Zynara.Core.Agents;

namespace Zynara.Agents;

/// <summary>Static <see cref="IAgentRoster"/> — the four reasoning steps and the implementation behind each.</summary>
internal sealed class AgentRoster(string implementation, string version) : IAgentRoster
{
    private static readonly string[] Steps =
        { "needs-auth", "evidence-gap", "claims-extraction", "appeal-builder", "critic" };

    public IReadOnlyList<AgentAttribution> Describe() =>
        Steps.Select(s => new AgentAttribution(s, s + "-agent", implementation, version)).ToList();

    public static AgentRoster Stub() => new("stub", "deterministic");
    public static AgentRoster Foundry(string model) => new("foundry", "hosted/" + model);
}
