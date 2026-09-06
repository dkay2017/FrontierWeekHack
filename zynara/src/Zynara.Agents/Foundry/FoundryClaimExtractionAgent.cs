using System.Text.Json;
using Zynara.Core.Agents;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>claims-extraction</c> backed by the hosted agent. Returns the list of
/// assertions the note makes; <c>ContradictionCheck</c> (deterministic) pairs
/// them. A non-parseable reply yields no claims — the deterministic step then
/// simply finds no contradiction, which is the safe default here (the reviewer
/// still sees the criterion-level evidence).
/// </summary>
public sealed class FoundryClaimExtractionAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IClaimExtractionAgent
{
    public async Task<ClaimSet> ExtractAsync(string clinicalNote, CancellationToken ct = default)
    {
        var inv = await client.InvokeAsync(
            options.ClaimsAgentName, "Clinical note:\n" + (clinicalNote ?? ""), toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.ClaimsAgentName, options.Model, inv, requestId: null, ct);

        return Parse(inv.Text);
    }

    internal static ClaimSet Parse(string reply)
    {
        if (AgentJson.FirstObject(reply) is not { } obj ||
            !obj.TryGetProperty("claims", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return ClaimSet.Empty;

        var claims = new List<Claim>();
        foreach (var c in arr.EnumerateArray())
        {
            if (c.ValueKind != JsonValueKind.Object) continue;
            var text = AgentJson.String(c, "text");
            var subject = AgentJson.String(c, "subject").Trim().ToLowerInvariant();
            var negated = c.TryGetProperty("negated", out var n) &&
                          (n.ValueKind == JsonValueKind.True ||
                           (n.ValueKind == JsonValueKind.String && n.GetString()?.Trim().ToLowerInvariant() == "true"));
            if (!string.IsNullOrWhiteSpace(text) && !string.IsNullOrWhiteSpace(subject))
                claims.Add(new Claim(text, subject, negated));
        }
        return new ClaimSet(claims);
    }
}
