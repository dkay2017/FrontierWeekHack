using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>precedent-strategist</c> twin — the differentiator. Recommends
/// submit / strengthen / appeal from the recorded outcomes of the matched
/// precedents, and produces a templated appeal draft when a denial has occurred.
/// </summary>
public sealed class StubPrecedentStrategistAgent : IPrecedentStrategistAgent
{
    public Task<StrategyRecommendation> RecommendAsync(
        Request request,
        EvidenceGapAssessment gap,
        IReadOnlyList<PrecedentMatch> shortlist,
        CancellationToken ct = default)
    {
        var won = shortlist.Where(m => m.Precedent.AppealOutcome == AppealOutcome.AppealWon).ToList();
        var lost = shortlist.Count(m => m.Precedent.AppealOutcome == AppealOutcome.AppealLost);
        var winRate = won.Count + lost > 0 ? (double)won.Count / (won.Count + lost) : 0d;
        var approvedAsSubmitted = shortlist.Where(m => m.Precedent.InitiallyApproved).ToList();

        var denied = !string.IsNullOrWhiteSpace(request.DenialLetter);
        var missing = gap.WithStatus(CriterionStatus.Missing).Count() + gap.WithStatus(CriterionStatus.Partial).Count();
        // On a denial we lean on the cases that won on appeal; on a fresh
        // submission the honest comparables are the ones approved as submitted.
        var cited = won.Take(3).Select(m => m.Precedent.CaseId).ToList();

        StrategyVerdict verdict;
        string text;
        string? draft = null;

        if (denied)
        {
            verdict = winRate >= 0.5 && won.Count > 0 ? StrategyVerdict.Appeal : StrategyVerdict.Strengthen;
            text = won.Count > 0
                ? $"{won.Count} comparable case(s) won on appeal (win rate {winRate:P0}). " +
                  (verdict == StrategyVerdict.Appeal
                      ? "Recommend appeal, citing the precedents below."
                      : "Recommend strengthening the record before re-appealing.")
                : "No comparable case has won on appeal on record — strengthen the record first.";
            if (verdict == StrategyVerdict.Appeal)
                draft = BuildDraft(request, won.Take(3).ToList());
        }
        else if (missing == 0 && !gap.AnyContradiction)
        {
            verdict = StrategyVerdict.Submit;
            cited = approvedAsSubmitted.Take(3).Select(m => m.Precedent.CaseId).ToList();
            text = approvedAsSubmitted.Count > 0
                ? $"No evidence gaps; {approvedAsSubmitted.Count} comparable case(s) were approved as " +
                  "submitted on this policy — submit."
                : "No evidence gaps and no denial on record — submit.";
        }
        else
        {
            verdict = StrategyVerdict.Strengthen;
            text = $"{missing} criterion(a) undocumented" +
                   (gap.AnyContradiction ? " and contradictory evidence present" : "") +
                   " — close the gaps with the clinician before submitting.";
        }

        return Task.FromResult(new StrategyRecommendation(verdict, cited, draft, text));
    }

    private static string BuildDraft(Request request, IReadOnlyList<PrecedentMatch> matches)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Re: appeal of the denied prior-authorisation request for {request.Procedure}.");
        sb.AppendLine();
        sb.AppendLine(
            "We respectfully appeal this denial. The clinical record meets the plan's published " +
            "criteria for this procedure, and the payer's own recorded outcomes support authorisation:");
        foreach (var m in matches)
        {
            var p = m.Precedent;
            var clauses = p.ClausesCited.Count > 0 ? string.Join(", ", p.ClausesCited) : "the same criteria";
            sb.AppendLine($"  - Case {p.CaseId} ({p.DecidedOn:yyyy-MM-dd}, {m.Similarity:0.00} similarity): " +
                          $"a comparable fact pattern, initially denied then authorised on appeal under {clauses}.");
        }
        sb.AppendLine();
        sb.AppendLine("We ask that the authorisation be granted. [Reviewer to add case-specific detail.]");
        return sb.ToString().TrimEnd();
    }
}
