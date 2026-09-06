using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 3 (the differentiator's deterministic half). Filters precedent metadata to
/// the same payer + procedure + region, ranks it by fact-pattern similarity to the
/// case in hand, and hands the top shortlist — with the similarity score and the
/// facts that matched — to the <c>precedent-strategist</c> agent. It also derives the
/// precedent-support level the Gate consumes.
/// Ranking is deterministic: token overlap between the clinical note (or denial
/// letter) and each precedent's narrative, a bonus for a shared denial reason code,
/// and a bonus for a case that won on appeal.
/// </summary>
public sealed class AppealMatch(IZynaraStore store, IPrecedentStrategistAgent agent)
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

        // A precedent that *lost* on appeal is only decision-relevant when we are
        // weighing whether to appeal. On a fresh submission it is noise, and the
        // Critic rightly refuses to let a loss stand in as positive support.
        var isAppeal = !string.IsNullOrWhiteSpace(request.DenialLetter);
        var relevant = isAppeal
            ? candidates
            : candidates.Where(p => p.AppealOutcome != AppealOutcome.AppealLost);

        var shortlist = relevant
            .Select(p => Build(p, caseTokens, denialCodes))
            .OrderByDescending(m => m.Similarity)
            .ThenByDescending(m => m.Precedent.DecidedOn)
            .Take(ShortlistSize)
            .ToList();

        var recommendation = await agent.RecommendAsync(request, gap, shortlist, ct);
        var support = DeriveSupport(shortlist);

        return new AppealMatchResult(shortlist, recommendation, support);
    }

    private static PrecedentMatch Build(
        Precedent p, HashSet<string> caseTokens, HashSet<string> denialCodes)
    {
        var pTokens = Tokens(p.FactPattern);
        var matched = caseTokens.Intersect(pTokens).OrderBy(t => t).ToList();

        var overlap = caseTokens.Count == 0 || pTokens.Count == 0
            ? 0d
            : (double)matched.Count / Math.Max(caseTokens.Count, pTokens.Count);

        var codeBonus = p.DenialReasonCodes.Any(denialCodes.Contains) ? 0.35 : 0d;
        var favourableBonus = IsFavourable(p) ? 0.15 : 0d;

        return new PrecedentMatch(p, Math.Round(overlap + codeBonus + favourableBonus, 3), matched);
    }

    /// <summary>
    /// A precedent that helps the case: approved as submitted, or a denial that
    /// was overturned on appeal. A lost appeal is <b>not</b> favourable.
    /// </summary>
    private static bool IsFavourable(Precedent p) =>
        p.InitiallyApproved || p.AppealOutcome == AppealOutcome.AppealWon;

    private static PrecedentSupport DeriveSupport(IReadOnlyList<PrecedentMatch> shortlist)
    {
        if (shortlist.Count == 0)
            return PrecedentSupport.None;

        var favourable = shortlist.Count(m => IsFavourable(m.Precedent));
        var strongMatch = shortlist.Any(m => m.Similarity >= 0.35);

        return (favourable, strongMatch) switch
        {
            (>= 2, true) => PrecedentSupport.Strong,
            (>= 1, _) => PrecedentSupport.Moderate,
            (0, true) => PrecedentSupport.Weak,
            _ => PrecedentSupport.Weak,
        };
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

        return denialLetter
            .Split(Split, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length is >= 2 and <= 8 && t.Any(char.IsDigit) && t.All(c => char.IsLetterOrDigit(c) || c == '-'))
            .Select(t => t.ToUpperInvariant())
            .ToHashSet();
    }
}
