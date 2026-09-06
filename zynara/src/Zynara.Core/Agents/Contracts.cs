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
// Returns which criteria are documented / missing / contradicted. The readiness
// score the Gate uses is derived from this deterministically by EvidenceGapMatch,
// not taken from the model.
// ---------------------------------------------------------------------------
public interface IEvidenceGapAgent
{
    Task<EvidenceGapAssessment> AssessAsync(
        string clinicalNote, Criteria criteria, CancellationToken ct = default);
}

/// <summary>The agent's reading of the note against the criteria — criterion ids, not prose, in the lists.</summary>
public sealed record EvidenceGapAssessment(
    IReadOnlyList<string> Met,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> Conflicts,
    string Text)
{
    /// <summary>Every id referenced must be a real criterion id, and each id classified at most once.</summary>
    public EvidenceGapAssessment Validate(Criteria criteria)
    {
        var known = criteria.Items.Select(c => c.Id).ToHashSet();
        var seen = new HashSet<string>();
        foreach (var id in Met.Concat(Missing).Concat(Conflicts))
        {
            if (!known.Contains(id))
                throw new ArgumentException($"EvidenceGapAssessment references unknown criterion id '{id}'.");
            if (!seen.Add(id))
                throw new ArgumentException($"EvidenceGapAssessment classifies criterion '{id}' more than once.");
        }
        return this;
    }
}

// ---------------------------------------------------------------------------
// appeal-builder (the differentiator) — matches the case to precedents that won,
// recommends submit / strengthen / appeal from their recorded outcomes, and drafts
// the appeal argument when a denial has occurred.
// ---------------------------------------------------------------------------
public enum AppealVerdict
{
    /// <summary>Gap-checked and ready — send it.</summary>
    Submit,

    /// <summary>Fixable gaps — go back to the clinician before submitting.</summary>
    Strengthen,

    /// <summary>Denied, and the fact pattern historically wins on appeal — appeal it.</summary>
    Appeal,
}

public interface IAppealBuilderAgent
{
    Task<AppealRecommendation> RecommendAsync(
        Request request,
        EvidenceGapAssessment gap,
        IReadOnlyList<Precedent> shortlist,
        CancellationToken ct = default);
}

/// <summary><c>AppealDraft</c> is populated only when <c>request.DenialLetter</c> is set.</summary>
public sealed record AppealRecommendation(
    AppealVerdict Verdict,
    IReadOnlyList<string> CitedPrecedentIds,
    string? AppealDraft,
    string Text);

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
