namespace TireForge.Core.Agents;

/// <summary>
/// Records a hosted-agent invocation's token usage for cost metering (Decision D13).
/// The real Foundry agents call this after every invocation; a no-op implementation
/// is used with the stubs / in tests.
/// </summary>
public interface IAgentCallRecorder
{
    Task RecordAsync(AgentCallUsage usage, CancellationToken ct = default);
}

/// <summary>What one hosted-agent invocation cost.</summary>
public sealed record AgentCallUsage(
    string AgentName,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int ToolCalls,
    string? ReadingId,
    string? TraceId);

/// <summary>Records nothing — the default when no data layer is wired (stubs, unit tests).</summary>
public sealed class NullAgentCallRecorder : IAgentCallRecorder
{
    public Task RecordAsync(AgentCallUsage usage, CancellationToken ct = default) => Task.CompletedTask;
}
