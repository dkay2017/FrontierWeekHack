namespace Zynara.Core.Agents;

/// <summary>
/// Records a hosted-agent invocation's token usage for the cost meter
/// (Architecture §12 · TDD §7 — £/$ per request · per agent · per day). The Foundry
/// agents call this after every invocation; a no-op is used with the stubs and in tests.
/// </summary>
public interface IAgentCallRecorder
{
    Task RecordAsync(AgentCallUsage usage, CancellationToken ct = default);
}

/// <summary>What one hosted-agent invocation cost — persisted to the <c>agentCalls</c> container.</summary>
public sealed record AgentCallUsage(
    string AgentName,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int ToolCalls,
    string? RequestId,
    string? TraceId);

/// <summary>Records nothing — the default when no data layer is wired (stubs, unit tests).</summary>
public sealed class NullAgentCallRecorder : IAgentCallRecorder
{
    public Task RecordAsync(AgentCallUsage usage, CancellationToken ct = default) => Task.CompletedTask;
}
