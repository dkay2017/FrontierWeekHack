using Zynara.Core.Model;

namespace Zynara.Eval;

/// <summary>
/// The "one generalist prompt" straw man for the comparison in TDD §3.1. It does
/// everything in a single naive pass: score every criterion by keyword hit, and
/// auto-submit when they all hit. It has no notion of a *mandatory* criterion, no
/// contradiction check, and never abstains — exactly the failure modes the
/// multi-agent design (Critic + structured decision model) is built to avoid.
/// Deterministic, so the comparison is repeatable in CI.
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
        return allHit ? GateRoute.AutoSubmit : GateRoute.HumanReview;
    }
}
