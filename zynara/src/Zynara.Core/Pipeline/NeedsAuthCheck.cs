using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Diagnostics;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 1. Resolves the payer + plan rule set for this procedure and region, and
/// decides — deterministically — whether prior authorisation is required and under
/// which code. The <c>needs-auth</c> agent is called only to narrate genuinely
/// ambiguous plan language (or the absence of a rule); it never owns the yes/no.
/// </summary>
public sealed class NeedsAuthCheck(IZynaraStore store, INeedsAuthAgent agent)
{
    public async Task<NeedsAuthResult> RunAsync(Request request, CancellationToken ct = default)
    {
        using var spoke = ZynaraTelemetry.StartSpoke("needs-auth");

        var rules = await store.GetPolicyRulesAsync(
            request.PayerPlan, request.Region, request.Procedure, ct);

        var requires = rules.Where(r => r.AuthRequired).ToList();
        var doesNot = rules.Where(r => !r.AuthRequired).ToList();

        // Unambiguous "not required" — the only path that stops the pipeline.
        if (requires.Count == 0 && doesNot.Count > 0)
            return new NeedsAuthResult(
                AuthRequired: false,
                RequiredCode: null,
                PolicyRef: doesNot[0].PolicyRef,
                Ambiguous: false,
                Note: $"{request.PayerPlan} does not require prior authorisation for " +
                      $"{request.Procedure} ({doesNot[0].PolicyRef}).");

        // Unambiguous "required".
        if (requires.Count > 0 && doesNot.Count == 0)
            return new NeedsAuthResult(
                AuthRequired: true,
                RequiredCode: requires[0].RequiredCode,
                PolicyRef: requires[0].PolicyRef,
                Ambiguous: false,
                Note: $"{request.PayerPlan} requires prior authorisation for {request.Procedure} " +
                      $"(code {requires[0].RequiredCode}, {requires[0].PolicyRef}).");

        // No rule at all, or rules disagree — ask the agent to explain, and default
        // to "required" (the safe side: a wasted submission beats an auto-denial).
        spoke?.SetTag("zynara.ambiguous", true);
        using var invoke = ZynaraTelemetry.StartAgentInvoke("needs-auth");
        var note = await agent.ExplainAsync(request, rules, ct);
        return new NeedsAuthResult(
            AuthRequired: true,
            RequiredCode: requires.FirstOrDefault()?.RequiredCode,
            PolicyRef: requires.FirstOrDefault()?.PolicyRef ?? doesNot.FirstOrDefault()?.PolicyRef,
            Ambiguous: true,
            Note: note);
    }
}
