using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>precedent-strategist</c> backed by the hosted agent — the differentiator. Reasons
/// over the ranked precedent shortlist, recommends submit / strengthen / appeal,
/// and drafts the appeal when a denial has occurred. The recommendation is
/// advisory: the deterministic Gate still decides ready-to-submit vs. review.
/// Citations and the verdict are sanitised against the shortlist.
/// </summary>
public sealed class FoundryPrecedentStrategistAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IPrecedentStrategistAgent
{
    public async Task<StrategyRecommendation> RecommendAsync(
        Request request,
        EvidenceGapAssessment gap,
        IReadOnlyList<PrecedentMatch> shortlist,
        CancellationToken ct = default)
    {
        var denied = !string.IsNullOrWhiteSpace(request.DenialLetter);
        var missing = gap.WithStatus(CriterionStatus.Missing).Count();
        var contradicted = gap.WithStatus(CriterionStatus.Contradicted).Count();

        var prompt = new StringBuilder()
            .AppendLine($"Procedure: {request.Procedure} · payer {request.PayerPlan} · region {request.Region}.")
            .AppendLine($"Evidence gap: {missing} missing, {contradicted} contradicted. {gap.Summary}")
            .AppendLine(denied ? $"Denial letter: {request.DenialLetter}" : "No denial yet.")
            .AppendLine()
            .AppendLine("Precedent shortlist (most similar first):");
        foreach (var m in shortlist)
        {
            var p = m.Precedent;
            prompt.AppendLine(
                $"  [{p.CaseId}] similarity {m.Similarity:0.00} — {p.FactPattern} — " +
                $"{(p.InitiallyApproved ? "approved first time" : "initially denied")}, " +
                $"appeal {p.AppealOutcome}" +
                (p.ClausesCited.Count > 0 ? $", clauses {string.Join("/", p.ClausesCited)}" : "") +
                (p.DenialReasonCodes.Count > 0 ? $", codes {string.Join("/", p.DenialReasonCodes)}" : ""));
        }

        var inv = await client.InvokeAsync(options.PrecedentStrategistAgentName, prompt.ToString(), toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.PrecedentStrategistAgentName, options.Model, inv, request.Id, ct);

        return FoundryResponse.ParseStrategy(inv.Text, shortlist, denied);
    }
}
