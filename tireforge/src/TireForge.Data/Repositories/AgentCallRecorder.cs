using TireForge.Core.Agents;
using TireForge.Core.Model;

namespace TireForge.Data.Repositories;

/// <summary>
/// Writes one <see cref="AgentCall"/> row per hosted-agent invocation (Decision D13).
/// </summary>
public sealed class AgentCallRecorder(TireForgeDbContext db) : IAgentCallRecorder
{
    public async Task RecordAsync(AgentCallUsage usage, CancellationToken ct = default)
    {
        db.AgentCalls.Add(new AgentCall
        {
            Id = Ids.AgentCall(DateTimeOffset.UtcNow),
            AgentName = usage.AgentName,
            Model = usage.Model,
            TraceId = usage.TraceId,
            ReadingId = usage.ReadingId,
            PromptTokens = usage.PromptTokens,
            CompletionTokens = usage.CompletionTokens,
            ToolCalls = usage.ToolCalls,
            At = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync(ct);
    }
}
