using Zynara.Core.Model;

namespace Zynara.Eval;

/// <summary>
/// The deterministic keyword straw man — the <b>fallback</b> baseline, used only
/// until <see cref="GeneralistBaselineLlm"/> fixtures are captured for every case.
/// It scores each criterion by keyword hit and marks the case ready when they all
/// hit, with no notion of a mandatory criterion, no contradiction check, and no
/// abstention. Deterministic, so CI is repeatable. Once
/// <c>baseline-fixtures/</c> exists the report uses the credible single-generalist
/// LLM instead (see <c>BASELINE.md</c>).
/// </summary>
public static class GeneralistBaseline
{
    private static readonly char[] Split = { ' ', ',', '.', ';', ':', '(', ')', '/', '-' };

    public static GateRoute Route(EvalCase c)
    {
        var note = c.Request.ClinicalNote.ToLowerInvariant();
        var denied = !string.IsNullOrWhiteSpace(c.Request.DenialLetter);

        var hits = c.Criteria.Count(cr =>
        {
            var keywords = cr.Text.ToLowerInvariant()
                .Split(Split, StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 5)
                .ToList();
            return keywords.Count == 0 || keywords.Any(k => note.Contains(k));
        });

        var allHit = hits == c.Criteria.Count;

        // Naive: complete-looking → auto-submit; otherwise → a human. No abstain.
        if (denied)
            return GateRoute.HumanReview;
        return allHit ? GateRoute.ReadyToSubmit : GateRoute.HumanReview;
    }
}
