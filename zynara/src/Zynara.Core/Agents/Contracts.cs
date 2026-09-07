using Zynara.Core.Model;

namespace Zynara.Core.Agents;

// The five agent ports and their structured outputs. The deterministic pipeline
// (the "spokes": NeedsAuthCheck / EvidenceGapMatch / AppealMatch / ExpiryMath /
// PolicyDiff) depends on these; Zynara.Agents supplies a stub twin and — swapped in
// by ZYNARA_AGENTS=foundry — the hosted Foundry implementations.
//
// Hybrid principle (Architecture §7 / TDD §9): deterministic code owns every value
// that drives a decision or an action; an agent only produces prose and judgement
// over unstructured text.

// ---------------------------------------------------------------------------
// needs-auth — plain-language reading of ambiguous plan text.
// NeedsAuthCheck owns the {authRequired, code, policyRef}; this agent is called
// only when the resolved rule set is ambiguous, and returns a short explanation.
// ---------------------------------------------------------------------------
public interface INeedsAuthAgent
{
    Task<string> ExplainAsync(
        Request request, IReadOnlyList<PolicyRule> candidates, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// evidence-gap — reads the free-text clinical note against the written criteria.
// Returns a per-criterion finding (documented / partial / missing / contradicted)
// with the supporting clinical statement, plus an overall evidence-quality call.
// EvidenceGapMatch and the Gate turn this into the structured decision model —
// no single averaged "readiness" number (evaluator finding #3).
// ---------------------------------------------------------------------------
public interface IEvidenceGapAgent
{
    Task<EvidenceGapAssessment> AssessAsync(
        string clinicalNote, Criteria criteria, CancellationToken ct = default);
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CriterionStatus
{
    /// <summary>The note clearly satisfies this criterion.</summary>
    Documented,

    /// <summary>Some evidence, but not enough to call it satisfied.</summary>
    Partial,

    /// <summary>The note is silent on this criterion.</summary>
    Missing,

    /// <summary>The note contains evidence that contradicts this criterion.</summary>
    Contradicted,
}

/// <summary>Overall quality of the clinical evidence supplied — drives the abstain route.</summary>
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum EvidenceQuality
{
    High,
    Medium,
    Low,
}

/// <summary>One criterion's finding. <see cref="Evidence"/> quotes the clinical statement the agent relied on.</summary>
public sealed record CriterionFinding(
    string CriterionId,
    CriterionStatus Status,
    string? Evidence);

/// <summary>The agent's structured reading of the note against the criteria.</summary>
public sealed record EvidenceGapAssessment(
    IReadOnlyList<CriterionFinding> Findings,
    EvidenceQuality Quality,
    string Summary)
{
    public IEnumerable<string> WithStatus(CriterionStatus s) =>
        Findings.Where(f => f.Status == s).Select(f => f.CriterionId);

    public bool AnyContradiction => Findings.Any(f => f.Status == CriterionStatus.Contradicted);

    /// <summary>Every finding must reference a real criterion, and each criterion appears exactly once.</summary>
    public EvidenceGapAssessment Validate(Criteria criteria)
    {
        var known = criteria.Items.Select(c => c.Id).ToHashSet();
        var seen = new HashSet<string>();
        foreach (var f in Findings)
        {
            if (!known.Contains(f.CriterionId))
                throw new ArgumentException($"EvidenceGapAssessment references unknown criterion id '{f.CriterionId}'.");
            if (!seen.Add(f.CriterionId))
                throw new ArgumentException($"EvidenceGapAssessment classifies criterion '{f.CriterionId}' more than once.");
        }
        if (seen.Count != known.Count)
            throw new ArgumentException("EvidenceGapAssessment must classify every criterion exactly once.");
        return this;
    }
}

// ---------------------------------------------------------------------------
// precedent-strategist (the differentiator) — matches the case to precedents that won,
// recommends submit / strengthen / appeal from their recorded outcomes, and drafts
// the appeal argument when a denial has occurred.
// ---------------------------------------------------------------------------
[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum StrategyVerdict
{
    /// <summary>Gap-checked and ready — send it.</summary>
    Submit,

    /// <summary>Fixable gaps — go back to the clinician before submitting.</summary>
    Strengthen,

    /// <summary>Denied, and the fact pattern historically wins on appeal — appeal it.</summary>
    Appeal,
}

public interface IPrecedentStrategistAgent
{
    Task<StrategyRecommendation> RecommendAsync(
        Request request,
        EvidenceGapAssessment gap,
        IReadOnlyList<PrecedentMatch> shortlist,
        CancellationToken ct = default);
}

/// <summary>
/// One ranked precedent — carries the deterministic similarity score and the facts
/// that matched, so the reviewer UI can show *why* it is comparable (evaluator finding #2).
/// </summary>
public sealed record PrecedentMatch(
    Precedent Precedent,
    double Similarity,
    IReadOnlyList<string> MatchedFacts);

/// <summary><c>AppealDraft</c> is populated only when <c>request.DenialLetter</c> is set.</summary>
public sealed record StrategyRecommendation(
    StrategyVerdict Verdict,
    IReadOnlyList<string> CitedPrecedentIds,
    string? AppealDraft,
    string Text);

// ---------------------------------------------------------------------------
// critic — the second reasoning agent in the loop (evaluator P0-2). It does not
// build anything; it tries to *disprove* the recommendation the pipeline has
// assembled, and it can force the case to a human or to abstention. This is what
// makes the system multi-agent rather than five specialised prompts: agents
// reason and challenge one another, deterministic code decides, humans authorise.
// ---------------------------------------------------------------------------
public interface ICriticAgent
{
    Task<CriticReview> ReviewAsync(CriticContext context, CancellationToken ct = default);
}

/// <summary>Everything the Critic weighs — the assembled case, before the Gate.</summary>
public sealed record CriticContext(
    Request Request,
    string NeedsAuthNote,
    EvidenceGapAssessment Evidence,
    IReadOnlyList<string> UnmetMandatory,
    IReadOnlyList<PrecedentMatch> Precedents,
    StrategyRecommendation Recommendation)
{
    /// <summary>Claims that conflict within the clinical note (P2-3) — check 4 also weighs these.</summary>
    public IReadOnlyList<ClaimConflict> NoteConflicts { get; init; } = Array.Empty<ClaimConflict>();
}

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum CriticVerdict
{
    /// <summary>No material concern — proceed to the Gate.</summary>
    Clear,

    /// <summary>Minor concerns — proceed, but the case can no longer auto-submit.</summary>
    Concerns,

    /// <summary>A material problem — the case must go to a human.</summary>
    Block,

    /// <summary>The recommendation is not supportable — the system should decline to advise.</summary>
    Abstain,
}

/// <summary>One thing the Critic challenged.</summary>
public sealed record CriticFlag(string Check, string Concern);

/// <summary>
/// The Critic's verdict on the assembled case. The seven checks (review §11):
/// every claim supported? · cited clause supports the claim? · precedents genuinely
/// comparable? · contradictory evidence? · a mandatory criterion missing? ·
/// recommendation stronger than the evidence allows? · should the system abstain?
/// </summary>
public sealed record CriticReview(
    CriticVerdict Verdict,
    IReadOnlyList<CriticFlag> Flags,
    string Summary);

// ---------------------------------------------------------------------------
// expiry-watch — narrates a cross-system expiry risk. ExpiryMath owns the date
// arithmetic and the margin; the agent only phrases the alert.
// ---------------------------------------------------------------------------
public interface IExpiryWatchAgent
{
    Task<string> NarrateAsync(
        AuthRecord auth, DateOnly procedureDate, int daysOfMargin, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// policy-drift — explains the operational impact of a criteria change. PolicyDiff
// owns the added/removed criteria; the agent explains what it means for in-flight work.
// ---------------------------------------------------------------------------
public interface IPolicyDriftAgent
{
    Task<string> ExplainImpactAsync(
        string policyRef,
        IReadOnlyList<string> addedCriteria,
        IReadOnlyList<string> removedCriteria,
        IReadOnlyList<string> affectedTemplates,
        CancellationToken ct = default);
}
