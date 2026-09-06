using Zynara.Core.Agents;

namespace Zynara.Agents.Stubs;

/// <summary>
/// Deterministic <c>claims-extraction</c> twin. Splits the note into sentences and
/// emits one <see cref="Claim"/> per sentence — subject = the most salient clinical
/// token, negated = a negation sits just before it. Crude, but enough for
/// <c>ContradictionCheck</c> to pair a "did X" claim against a "did not X" claim
/// about the same subject. The Foundry agent does the real reading.
/// </summary>
public sealed class StubClaimExtractionAgent : IClaimExtractionAgent
{
    private static readonly char[] SentenceBreak = { '.', '\n', '!', '?', ';' };
    private static readonly char[] WordBreak = { ' ', ',', '(', ')', '/', ':', '-' };

    private static readonly HashSet<string> Stop = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "has", "have", "been", "was", "were", "that", "this",
        "patient", "reports", "reported", "states", "noted", "there", "their", "they", "them",
        "which", "would", "could", "should", "about", "after", "before", "under", "over",
        "clinic", "clinical", "history", "examination", "attend", "attended",
    };

    private static readonly string[] Negators =
    {
        "no ", "not ", "never ", "without ", "denies ", "denied ", "unable to ", "declined ",
        "did not ", "could not ", "unable ", "absence of ", "negative for ", "ruled out ",
    };

    public Task<ClaimSet> ExtractAsync(string clinicalNote, CancellationToken ct = default)
    {
        var claims = new List<Claim>();

        foreach (var raw in (clinicalNote ?? "").Split(SentenceBreak, StringSplitOptions.RemoveEmptyEntries))
        {
            var sentence = raw.Trim();
            if (sentence.Length < 4) continue;
            var lower = sentence.ToLowerInvariant();

            var subject = sentence
                .Split(WordBreak, StringSplitOptions.RemoveEmptyEntries)
                .Select(w => w.Trim('.').ToLowerInvariant())
                .Where(w => w.Length >= 5 && !Stop.Contains(w) && w.Any(char.IsLetter))
                .OrderByDescending(w => w.Length)
                .FirstOrDefault();

            if (subject is null) continue;

            var at = lower.IndexOf(subject, StringComparison.Ordinal);
            var before = lower[..Math.Max(0, at)];
            var window = before.Length > 24 ? before[^24..] : before;
            var negated = Negators.Any(n => window.Contains(n)) || Negators.Any(lower.StartsWith);

            claims.Add(new Claim(sentence, subject, negated));
        }

        return Task.FromResult(new ClaimSet(claims));
    }
}
