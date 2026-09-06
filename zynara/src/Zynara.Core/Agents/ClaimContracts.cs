namespace Zynara.Core.Agents;

// claims-extraction — the first half of the contradiction-detection flow
// (evaluator finding #4). The agent pulls discrete factual assertions out of the
// clinical note; ContradictionCheck (deterministic) then looks for pairs that
// assert opposite things about the same subject. A detected contradiction is
// never silently resolved — the Gate routes it to a human.

public interface IClaimExtractionAgent
{
    Task<ClaimSet> ExtractAsync(string clinicalNote, CancellationToken ct = default);
}

/// <summary>One factual assertion from the note. <see cref="Subject"/> is the thing it is about.</summary>
public sealed record Claim(string Text, string Subject, bool Negated);

public sealed record ClaimSet(IReadOnlyList<Claim> Claims)
{
    public static readonly ClaimSet Empty = new(Array.Empty<Claim>());
}

/// <summary>Two claims about the same subject that point opposite ways.</summary>
public sealed record ClaimConflict(string Subject, Claim Affirmed, Claim Denied)
{
    public string Describe() =>
        $"the note both states and denies “{Subject}” — “{Affirmed.Text}” vs “{Denied.Text}”";
}
