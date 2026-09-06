using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>critic</c> twin. Runs the seven checks against the assembled
/// case and returns the worst verdict found. The real agent reasons over the prose
/// (does the cited clause actually support the claim, are the precedents genuinely
/// comparable); this one applies the checks it can verify structurally, tuned so
/// the Gate sees a realistic mix of Clear / Concerns / Block / Abstain.
/// </summary>
public sealed class StubCriticAgent : ICriticAgent
{
    private const double ComparableSimilarity = 0.30;

    public Task<CriticReview> ReviewAsync(CriticContext c, CancellationToken ct = default)
    {
        var flags = new List<CriticFlag>();
        var verdict = CriticVerdict.Clear;

        void Raise(CriticVerdict v, string check, string concern)
        {
            flags.Add(new CriticFlag(check, concern));
            if (v > verdict) verdict = v;
        }

        // 5 · a mandatory criterion missing
        if (c.UnmetMandatory.Count > 0)
            Raise(CriticVerdict.Block, "mandatory-criteria",
                $"mandatory criterion not documented: {string.Join(", ", c.UnmetMandatory)}");

        // 4 · contradictory evidence — against a criterion, or the note against itself
        if (c.Evidence.AnyContradiction)
            Raise(CriticVerdict.Block, "contradiction",
                "the clinical note contains evidence that contradicts a criterion");
        if (c.NoteConflicts.Count > 0)
            Raise(CriticVerdict.Block, "contradiction",
                c.NoteConflicts[0].Describe());

        // 3 · precedents genuinely comparable
        var comparable = c.Precedents.Any(p => p.Similarity >= ComparableSimilarity);
        if (c.Recommendation.Verdict == AppealVerdict.Appeal && !comparable)
            Raise(CriticVerdict.Concerns, "precedent-comparability",
                "no shortlisted precedent is clearly comparable to this case");

        // 6 · recommendation stronger than the evidence allows
        var won = c.Precedents.Count(p => p.Precedent.AppealOutcome == AppealOutcome.AppealWon);
        if (c.Recommendation.Verdict == AppealVerdict.Appeal && won == 0)
            Raise(CriticVerdict.Block, "recommendation-strength",
                "an appeal is recommended but no comparable case has won on appeal");
        if (c.Recommendation.Verdict is AppealVerdict.Submit or AppealVerdict.Appeal
            && c.Evidence.Quality == EvidenceQuality.Low)
            Raise(CriticVerdict.Concerns, "recommendation-strength",
                "the recommendation is firmer than Low-quality evidence supports");

        // 1 · every claim supported (structural proxy: an appeal draft that cites nothing)
        if (c.Recommendation.AppealDraft is { Length: > 0 } && c.Recommendation.CitedPrecedentIds.Count == 0)
            Raise(CriticVerdict.Concerns, "claim-support",
                "the appeal draft does not cite any precedent");

        // 7 · should the system abstain
        var noPrecedentSupport = won == 0 && !comparable;
        if (c.Evidence.Quality == EvidenceQuality.Low && noPrecedentSupport)
            Raise(CriticVerdict.Abstain, "abstention",
                "Low-quality evidence and no comparable precedent — not enough to make a reliable recommendation");

        var summary = verdict switch
        {
            CriticVerdict.Clear => "No material concern — the recommendation holds up.",
            CriticVerdict.Concerns => $"{flags.Count} concern(s) — proceed with a human, do not auto-submit.",
            CriticVerdict.Block => $"{flags.Count} material problem(s) — the case must go to a reviewer.",
            _ => "The recommendation is not supportable — the system should abstain.",
        };

        return Task.FromResult(new CriticReview(verdict, flags, summary));
    }
}
