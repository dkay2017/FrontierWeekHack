using Microsoft.Azure.Cosmos;
using Zynara.Core.Agents;

namespace Zynara.Data;

/// <summary>
/// Cosmos-backed cost meter (TDD §7). One document per hosted-agent invocation in
/// the <c>agentCalls</c> container, partitioned by day so "£/$ per agent per day"
/// is a single-partition aggregate.
/// </summary>
public sealed class CosmosAgentCallRecorder(Database db, TimeProvider? clock = null) : IAgentCallRecorder
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task RecordAsync(AgentCallUsage usage, CancellationToken ct = default)
    {
        var now = _clock.GetUtcNow();
        var day = now.ToString("yyyy-MM-dd");

        var doc = CosmosJson.WithId(
            new
            {
                usage.AgentName,
                usage.Model,
                usage.PromptTokens,
                usage.CompletionTokens,
                usage.ToolCalls,
                usage.RequestId,
                usage.TraceId,
                at = now,
                day,
            },
            id: Guid.NewGuid().ToString("n"),
            extra: ("day", day));

        await db.GetContainer(CosmosNames.AgentCalls)
            .CreateItemAsync(doc, new PartitionKey(day), cancellationToken: ct);
    }
}
