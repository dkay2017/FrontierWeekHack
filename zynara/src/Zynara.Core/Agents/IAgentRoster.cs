namespace Zynara.Core.Agents;

/// <summary>Which agent implementation backs one reasoning step, and its version — for the case audit trail.</summary>
public sealed record AgentAttribution(string Step, string Agent, string Implementation, string Version)
{
    public string Label => $"{Step} · {Implementation} {Version}";
}

/// <summary>
/// Names the agent implementation and version behind each reasoning step.
/// <c>Zynara.Agents</c> supplies it (stub or Foundry); <c>CaseService</c> writes
/// it into the case audit record (evaluator §13 — agent identity + version).
/// </summary>
public interface IAgentRoster
{
    IReadOnlyList<AgentAttribution> Describe();
}
