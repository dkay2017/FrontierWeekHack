using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>evidence-gap</c> twin. Scores each written criterion against the
/// clinical note by keyword overlap, and flags a contradiction when a negation sits
/// near the matched keywords. The real agent does genuine comprehension; this one is
/// tuned to produce a realistic met/missing spread for the Gate and the tests.
/// </summary>
public sealed class StubEvidenceGapAgent : IEvidenceGapAgent
{
    private static readonly string[] Stop =
    {
        "the", "and", "for", "with", "has", "have", "been", "was", "were", "that", "this",
        "any", "all", "not", "must", "should", "least", "prior", "days", "weeks", "such",
        "months", "documented", "evidence", "would", "could", "then", "than", "when",
        "from", "into", "each", "also", "tried", "some", "will", "shall", "been", "over",
        "of", "a", "an", "in", "on", "to", "or", "is",
    };

    private static readonly string[] Negators =
    {
        "no ", "not ", "without", "denies", "denied", "absence of", "negative for",
        "ruled out", "none", "n/a", "did not", "hasn't", "haven't", "no evidence",
    };

    public Task<EvidenceGapAssessment> AssessAsync(
        string clinicalNote, Criteria criteria, CancellationToken ct = default)
    {
        var note = (clinicalNote ?? "").ToLowerInvariant();
        var met = new List<string>();
        var missing = new List<string>();
        var conflicts = new List<string>();

        foreach (var c in criteria.Items)
        {
            var keywords = Keywords(c.Text);
            var hits = keywords.Count(k => note.Contains(k));
            // A criterion counts as addressed if any of its distinctive words appear
            // in the note — distinctive words (physiotherapy, radiculopathy, …) don't
            // show up unless the clinician actually documented that point.
            var covered = keywords.Count == 0 || hits >= 1;

            if (!covered)
            {
                missing.Add(c.Id);
                continue;
            }

            if (keywords.Any(k => Contradicted(note, k)))
                conflicts.Add(c.Id);
            else
                met.Add(c.Id);
        }

        var text = BuildText(criteria, met, missing, conflicts);
        var assessment = new EvidenceGapAssessment(met, missing, conflicts, text).Validate(criteria);
        return Task.FromResult(assessment);
    }

    private static List<string> Keywords(string criterionText) =>
        criterionText.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ';', ':', '(', ')', '/', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4 && !Stop.Contains(w))
            .Distinct()
            .ToList();

    private static bool Contradicted(string note, string keyword)
    {
        var i = note.IndexOf(keyword, StringComparison.Ordinal);
        while (i >= 0)
        {
            var start = Math.Max(0, i - 40);
            var window = note[start..i];
            if (Negators.Any(n => window.Contains(n)))
                return true;
            i = note.IndexOf(keyword, i + keyword.Length, StringComparison.Ordinal);
        }
        return false;
    }

    private static string BuildText(
        Criteria criteria, List<string> met, List<string> missing, List<string> conflicts)
    {
        var sb = new StringBuilder(
            $"{met.Count}/{criteria.Items.Count} criteria documented for {criteria.Procedure} ({criteria.PolicyRef}). ");
        if (missing.Count > 0)
            sb.Append($"Missing: {string.Join(", ", Describe(criteria, missing))}. ");
        if (conflicts.Count > 0)
            sb.Append($"Contradicted by the note: {string.Join(", ", Describe(criteria, conflicts))}. ");
        if (missing.Count == 0 && conflicts.Count == 0)
            sb.Append("No gaps — ready to submit.");
        return sb.ToString().TrimEnd();
    }

    private static IEnumerable<string> Describe(Criteria criteria, IEnumerable<string> ids) =>
        ids.Select(id => criteria.Items.First(c => c.Id == id).Text);
}
