using Zynara.Core.Agents;
using Zynara.Core.Diagnostics;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// The contradiction-detection flow (evaluator finding #4). The
/// <c>claims-extraction</c> agent pulls discrete assertions out of the clinical
/// note; this deterministic step then pairs claims about the <b>same subject</b>
/// that point opposite ways. A detected conflict is never silently resolved — it
/// is handed to the Gate (→ HumanReview) and surfaced to the reviewer.
/// </summary>
public sealed class ContradictionCheck(IClaimExtractionAgent agent)
{
    public async Task<ContradictionResult> RunAsync(Request request, CancellationToken ct = default)
    {
        using var spoke = ZynaraTelemetry.StartSpoke("claims-extraction");

        ClaimSet set;
        using (ZynaraTelemetry.StartAgentInvoke("claims-extraction"))
            set = await agent.ExtractAsync(request.ClinicalNote ?? "", ct);

        var conflicts = new List<ClaimConflict>();
        var bySubject = set.Claims
            .Where(c => !string.IsNullOrWhiteSpace(c.Subject))
            .GroupBy(c => c.Subject, StringComparer.OrdinalIgnoreCase);

        foreach (var group in bySubject)
        {
            var affirmed = group.Where(c => !c.Negated).ToList();
            var denied = group.Where(c => c.Negated).ToList();
            if (affirmed.Count > 0 && denied.Count > 0)
                conflicts.Add(new ClaimConflict(group.Key, affirmed[0], denied[0]));
        }

        return new ContradictionResult(set.Claims.Count, conflicts);
    }
}
