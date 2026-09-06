using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// Parses and sanitises the two structured agent replies. Pulled out of the agent
/// classes so the robustness rules are unit-testable without a live agent:
/// unknown ids dropped, unclassified criteria → Missing, a non-parseable reply →
/// a conservative fallback that routes to review, "appeal" only valid once denied.
/// </summary>
internal static class FoundryResponse
{
    /// <summary>evidence-gap reply → a valid <see cref="EvidenceGapAssessment"/> for these criteria.</summary>
    public static EvidenceGapAssessment ParseGap(string reply, Criteria criteria)
    {
        var known = criteria.Items.Select(c => c.Id).ToHashSet();
        var obj = AgentJson.FirstObject(reply);

        if (obj is not { } o)
            return new EvidenceGapAssessment(
                criteria.Items.Select(c => new CriterionFinding(c.Id, CriterionStatus.Missing, null)).ToList(),
                EvidenceQuality.Low,
                "Agent response could not be parsed — routed to human review.");

        var findings = new Dictionary<string, CriterionFinding>();
        Classify(o, "met", CriterionStatus.Documented, known, findings);
        Classify(o, "documented", CriterionStatus.Documented, known, findings);
        Classify(o, "partial", CriterionStatus.Partial, known, findings);
        Classify(o, "conflicts", CriterionStatus.Contradicted, known, findings);
        Classify(o, "contradicted", CriterionStatus.Contradicted, known, findings);
        Classify(o, "missing", CriterionStatus.Missing, known, findings);

        foreach (var id in known.Except(findings.Keys)) // anything unclassified is a gap
            findings[id] = new CriterionFinding(id, CriterionStatus.Missing, null);

        var quality = AgentJson.String(o, "quality").Trim().ToLowerInvariant() switch
        {
            "high" => EvidenceQuality.High,
            "medium" or "med" => EvidenceQuality.Medium,
            "low" => EvidenceQuality.Low,
            _ => Grade(findings.Values),
        };

        var summary = AgentJson.String(o, "summary");
        if (summary.Length == 0)
            summary = $"{findings.Values.Count(f => f.Status == CriterionStatus.Documented)}/{known.Count} criteria documented.";

        return new EvidenceGapAssessment(
            criteria.Items.Select(c => findings[c.Id]).ToList(), quality, summary);
    }

    /// <summary>appeal-builder reply → a sanitised <see cref="AppealRecommendation"/>.</summary>
    public static AppealRecommendation ParseAppeal(
        string reply, IReadOnlyList<PrecedentMatch> shortlist, bool denied)
    {
        var ids = shortlist.Select(m => m.Precedent.CaseId).ToHashSet();
        var obj = AgentJson.FirstObject(reply);

        if (obj is not { } o)
            return new AppealRecommendation(
                AppealVerdict.Strengthen, Array.Empty<string>(), null,
                "Agent response could not be parsed — routed to human review.");

        var verdict = AgentJson.String(o, "verdict").Trim().ToLowerInvariant() switch
        {
            "submit" => AppealVerdict.Submit,
            "appeal" when denied => AppealVerdict.Appeal,
            _ => AppealVerdict.Strengthen,
        };

        var cited = AgentJson.StringArray(o, "cited").Where(ids.Contains).Distinct().ToList();

        var draftRaw = AgentJson.String(o, "draft").Trim();
        var draft = verdict == AppealVerdict.Appeal && draftRaw.Length > 0 ? draftRaw : null;

        var rationale = AgentJson.String(o, "rationale");
        if (rationale.Length == 0)
            rationale = $"Recommendation: {verdict}.";

        return new AppealRecommendation(verdict, cited, draft, rationale);
    }

    private static void Classify(
        System.Text.Json.JsonElement o, string name, CriterionStatus status,
        HashSet<string> known, Dictionary<string, CriterionFinding> into)
    {
        foreach (var id in AgentJson.StringArray(o, name))
            if (known.Contains(id) && !into.ContainsKey(id))
                into[id] = new CriterionFinding(id, status, null);
    }

    private static EvidenceQuality Grade(IEnumerable<CriterionFinding> findings)
    {
        var list = findings.ToList();
        if (list.Count == 0) return EvidenceQuality.Low;
        var ratio = (double)list.Count(f => f.Status == CriterionStatus.Documented) / list.Count;
        return ratio switch { >= 0.8 => EvidenceQuality.High, >= 0.4 => EvidenceQuality.Medium, _ => EvidenceQuality.Low };
    }
}
