using System.Diagnostics;
using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>needs-auth</c> backed by the hosted agent. Returns the plain-language
/// explanation; the yes/no + code stay deterministic in <c>NeedsAuthCheck</c>.
/// </summary>
public sealed class FoundryNeedsAuthAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : INeedsAuthAgent
{
    public async Task<string> ExplainAsync(
        Request request, IReadOnlyList<PolicyRule> candidates, CancellationToken ct = default)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Request: {request.Procedure} · plan {request.PayerPlan} · region {request.Region}.")
            .AppendLine(candidates.Count == 0
                ? "No payer rule matched this procedure/plan/region."
                : "Matched payer rules:");
        foreach (var r in candidates)
            prompt.AppendLine(
                $"  - {(r.AuthRequired ? "auth REQUIRED" : "auth not required")} · " +
                $"code {(string.IsNullOrEmpty(r.RequiredCode) ? "—" : r.RequiredCode)} · ref {r.PolicyRef}" +
                (r.Notes is { Length: > 0 } ? $" · {r.Notes}" : ""));

        var inv = await client.InvokeAsync(options.NeedsAuthAgentName, prompt.ToString(), toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.NeedsAuthAgentName, options.Model, inv, request.Id, ct);
        return inv.Text.Trim();
    }
}

/// <summary>Shared cost-meter write for the Foundry agents.</summary>
internal static class Usage
{
    public static Task RecordAsync(
        IAgentCallRecorder recorder, string agentName, string model, AgentInvocation inv,
        string? requestId, CancellationToken ct) =>
        recorder.RecordAsync(new AgentCallUsage(
            agentName, model, inv.InputTokens, inv.OutputTokens, inv.ToolCalls,
            requestId, Activity.Current?.TraceId.ToString()), ct);
}
