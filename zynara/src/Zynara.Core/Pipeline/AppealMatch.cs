using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 3 (the differentiator's deterministic half). Filters precedent metadata to
/// the same payer + procedure + region, ranks it by fact-pattern similarity to the
/// case in hand, and hands the top shortlist to the <c>appeal-builder</c> agent.
/// Ranking is deterministic — token overlap between the clinical note (or denial
/// letter) and each precedent's fact-pattern narrative, plus a bonus for a shared
/// denial reason code.
/// </summary>
public sealed class AppealMatch(IZynaraStore store, IAppealBuilderAgent agent)
{
    private const int ShortlistSize = 5;

    private static readonly char[] Split = { ' ', ',', '.', ';', ':', '(', ')', '/', '-', '\n', '\r', '\t' };

    public async Task<AppealMatchResult> RunAsync(
        Request request, EvidenceGapAssessment gap, CancellationToken ct = default)
    {
        var candidates = await store.GetPrecedentsAsync(
            request.PayerPlan, request.Procedure, request.Region, ct);

        var caseTokens = Tokens($"{request.ClinicalNote} {request.DenialLetter}");
        var denialCodes = DenialCodes(request.DenialLetter);

        var shortlist = candidates
            .Select(p => (p, score: Score(p, caseTokens, denialCodes)))
            .OrderByDescending(x => x.score)
            .ThenByDescending(x => x.p.DecidedOn)
            .Take(ShortlistSize)
            .Select(x => x.p)
            .ToList();

        var recommendation = await agent.RecommendAsync(request, gap, shortlist, ct);
        return new AppealMatchResult(shortlist, recommendation);
    }

    private static double Score(Precedent p, HashSet<string> caseTokens, HashSet<string> denialCodes)
    {
        var pTokens = Tokens(p.FactPattern);
        var overlap = caseTokens.Count == 0 || pTokens.Count == 0
            ? 0d
            : (double)caseTokens.Intersect(pTokens).Count() / Math.Max(caseTokens.Count, pTokens.Count);

        var codeBonus = p.DenialReasonCodes.Any(denialCodes.Contains) ? 0.35 : 0d;
        var wonBonus = p.AppealOutcome == AppealOutcome.AppealWon ? 0.15 : 0d;

        return overlap + codeBonus + wonBonus;
    }

    private static HashSet<string> Tokens(string? text) =>
        (text ?? "").ToLowerInvariant()
            .Split(Split, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4)
            .ToHashSet();

    private static HashSet<string> DenialCodes(string? denialLetter)
    {
        if (string.IsNullOrWhiteSpace(denialLetter))
            return new HashSet<string>();

        // Reason codes look like MN-01, CO-197, 50: pull short upper-case / digit tokens.
        return denialLetter
            .Split(Split, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length is >= 2 and <= 8 && t.Any(char.IsDigit) && t.All(c => char.IsLetterOrDigit(c) || c == '-'))
            .Select(t => t.ToUpperInvariant())
            .ToHashSet();
    }
}
