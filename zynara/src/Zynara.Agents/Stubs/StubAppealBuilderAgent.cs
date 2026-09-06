using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>appeal-builder</c> twin — the differentiator. Recommends
/// submit / strengthen / appeal from the recorded outcomes of the matched
/// precedents, and produces a templated appeal draft when a denial has occurred.
/// The real agent writes the argument prose; the verdict logic is deterministic
/// either way.
/// </summary>
public sealed class StubAppealBuilderAgent : IAppealBuilderAgent
{
    public Task<AppealRecommendation> RecommendAsync(
        Request request,
        EvidenceGapAssessment gap,
        IReadOnlyList<Precedent> shortlist,
        CancellationToken ct = default)
    {
        var won = shortlist.Where(p => p.AppealOutcome == AppealOutcome.AppealWon).ToList();
        var lost = shortlist.Count(p => p.AppealOutcome == AppealOutcome.AppealLost);
        var winRate = won.Count + lost > 0 ? (double)won.Count / (won.Count + lost) : 0d;

        var denied = !string.IsNullOrWhiteSpace(request.DenialLetter);
        var cited = won.Take(3).Select(p => p.CaseId).ToList();

        AppealVerdict verdict;
        string text;
        string? draft = null;

        if (denied)
        {
            verdict = winRate >= 0.5 && won.Count > 0 ? AppealVerdict.Appeal : AppealVerdict.Strengthen;
            text = won.Count > 0
                ? $"{won.Count} comparable case(s) won on appeal (win rate {winRate:P0}). " +
                  (verdict == AppealVerdict.Appeal
                      ? "Recommend appeal, citing the precedents below."
                      : "Recommend strengthening the record before re-appealing.")
                : "No comparable case has won on appeal on record — strengthen the record first.";
            if (verdict == AppealVerdict.Appeal)
                draft = BuildDraft(request, won.Take(3).ToList());
        }
        else if (gap.Missing.Count == 0 && gap.Conflicts.Count == 0)
        {
            verdict = AppealVerdict.Submit;
            text = "No evidence gaps and comparable cases were approved first time — submit.";
        }
        else
        {
            verdict = AppealVerdict.Strengthen;
            text = $"{gap.Missing.Count} criterion(a) undocumented and {gap.Conflicts.Count} contradicted — " +
                   "close the gaps with the clinician before submitting.";
        }

        return Task.FromResult(new AppealRecommendation(verdict, cited, draft, text));
    }

    private static string BuildDraft(Request request, IReadOnlyList<Precedent> precedents)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Re: appeal of the denied prior-authorisation request for {request.Procedure}.");
        sb.AppendLine();
        sb.AppendLine(
            "We respectfully appeal this denial. The clinical record meets the plan's published " +
            "criteria for this procedure, and the payer's own recorded outcomes support authorisation:");
        foreach (var p in precedents)
        {
            var clauses = p.ClausesCited.Count > 0 ? string.Join(", ", p.ClausesCited) : "the same criteria";
            sb.AppendLine($"  - Case {p.CaseId} ({p.DecidedOn:yyyy-MM-dd}): a comparable fact pattern, " +
                          $"initially denied then authorised on appeal under {clauses}.");
        }
        sb.AppendLine();
        sb.AppendLine("We ask that the authorisation be granted. [Reviewer to add case-specific detail.]");
        return sb.ToString().TrimEnd();
    }
}
