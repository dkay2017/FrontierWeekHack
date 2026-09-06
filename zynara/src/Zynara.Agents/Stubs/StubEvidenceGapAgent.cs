using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>evidence-gap</c> twin. Classifies each written criterion against
/// the clinical note by keyword overlap, flags a contradiction when a negation sits
/// near the matched keywords, and grades overall evidence quality. Interchangeable
/// with the Foundry agent — they differ only in the prose and the accuracy.
/// </summary>
public sealed class StubEvidenceGapAgent : IEvidenceGapAgent
{
    private static readonly string[] Stop =
    {
        "the", "and", "for", "with", "has", "have", "been", "was", "were", "that", "this",
        "any", "all", "not", "must", "should", "least", "prior", "days", "weeks", "such",
        "months", "documented", "evidence", "would", "could", "then", "than", "when",
        "from", "into", "each", "also", "tried", "some", "will", "shall", "over",
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
        var findings = new List<CriterionFinding>();

        foreach (var c in criteria.Items)
        {
            var keywords = Keywords(c.Text);
            var hits = keywords.Where(k => note.Contains(k)).ToList();

            CriterionStatus status;
            string? evidence = null;

            if (hits.Count == 0)
            {
                status = CriterionStatus.Missing;
            }
            else if (hits.Any(k => Contradicted(note, k)))
            {
                status = CriterionStatus.Contradicted;
                evidence = Sentence(clinicalNote, hits[0]);
            }
            else
            {
                status = keywords.Count > 3 && hits.Count == 1 ? CriterionStatus.Partial : CriterionStatus.Documented;
                evidence = Sentence(clinicalNote, hits[0]);
            }

            findings.Add(new CriterionFinding(c.Id, status, evidence));
        }

        var quality = Grade(findings);
        var summary = BuildSummary(criteria, findings);
        return Task.FromResult(new EvidenceGapAssessment(findings, quality, summary));
    }

    private static EvidenceQuality Grade(IReadOnlyList<CriterionFinding> findings)
    {
        if (findings.Count == 0) return EvidenceQuality.Low;
        var documented = findings.Count(f => f.Status == CriterionStatus.Documented);
        var ratio = (double)documented / findings.Count;
        return ratio switch { >= 0.8 => EvidenceQuality.High, >= 0.4 => EvidenceQuality.Medium, _ => EvidenceQuality.Low };
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
            var window = note[Math.Max(0, i - 40)..i];
            if (Negators.Any(n => window.Contains(n)))
                return true;
            i = note.IndexOf(keyword, i + keyword.Length, StringComparison.Ordinal);
        }
        return false;
    }

    private static string? Sentence(string note, string keyword)
    {
        var lower = note.ToLowerInvariant();
        var i = lower.IndexOf(keyword, StringComparison.Ordinal);
        if (i < 0) return null;
        var start = note.LastIndexOfAny(new[] { '.', '\n', '!', '?' }, i) + 1;
        var end = note.IndexOfAny(new[] { '.', '\n', '!', '?' }, i);
        if (end < 0) end = note.Length;
        return note[start..end].Trim();
    }

    private static string BuildSummary(Criteria criteria, IReadOnlyList<CriterionFinding> findings)
    {
        var d = findings.Count(f => f.Status == CriterionStatus.Documented);
        var parts = new List<string> { $"{d}/{criteria.Items.Count} criteria documented" };

        var gaps = findings
            .Where(f => f.Status is CriterionStatus.Missing or CriterionStatus.Partial)
            .Select(f => criteria.Items.First(c => c.Id == f.CriterionId).Text);
        if (gaps.Any())
            parts.Add($"gaps: {string.Join("; ", gaps)}");

        var conflicts = findings
            .Where(f => f.Status == CriterionStatus.Contradicted)
            .Select(f => criteria.Items.First(c => c.Id == f.CriterionId).Text);
        if (conflicts.Any())
            parts.Add($"contradicted: {string.Join("; ", conflicts)}");

        return string.Join(". ", parts) + ".";
    }
}
