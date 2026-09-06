using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>needs-auth</c> twin. Describes what the matching plan rules say,
/// and flags disagreement. The real agent reads genuinely ambiguous plan language;
/// this one reports the rule set as-is. Interchangeable with the Foundry agent —
/// they differ only in the prose.
/// </summary>
public sealed class StubNeedsAuthAgent : INeedsAuthAgent
{
    public Task<string> ExplainAsync(
        Request request, IReadOnlyList<PolicyRule> candidates, CancellationToken ct = default)
    {
        if (candidates.Count == 0)
            return Task.FromResult(
                $"No published rule for {request.PayerPlan} covering {request.Procedure} " +
                $"in {request.Region}. Treat as prior-authorisation required and confirm with the payer.");

        var requires = candidates.Where(c => c.AuthRequired).ToList();
        var doesNot = candidates.Where(c => !c.AuthRequired).ToList();

        if (doesNot.Count == 0)
        {
            var r = requires[0];
            return Task.FromResult(
                $"{request.PayerPlan} requires prior authorisation for {request.Procedure} " +
                $"(code {r.RequiredCode}, {r.PolicyRef}). Proceed with a gap-checked submission.");
        }

        if (requires.Count == 0)
            return Task.FromResult(
                $"{request.PayerPlan} does not require prior authorisation for {request.Procedure} " +
                $"({doesNot[0].PolicyRef}). Log and stop — nothing to submit.");

        return Task.FromResult(
            $"Ambiguous: {requires.Count} rule(s) for {request.PayerPlan} require authorisation for " +
            $"{request.Procedure} ({string.Join(", ", requires.Select(r => r.PolicyRef))}) while " +
            $"{doesNot.Count} do not ({string.Join(", ", doesNot.Select(r => r.PolicyRef))}). " +
            "Default to required and confirm the plan variant with the payer.");
    }
}
