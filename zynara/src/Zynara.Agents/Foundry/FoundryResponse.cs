using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// Parses and sanitises the two structured agent replies. Pulled out of the agent
/// classes so the robustness rules (unknown ids dropped, unclassified criteria →
/// missing, "appeal" only valid once denied) are unit-testable without a live agent.
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
                Array.Empty<string>(), criteria.Items.Select(c => c.Id).ToList(), Array.Empty<string>(),
                "Agent response could not be parsed — routed to human review.");

        var claimed = new HashSet<string>();
        var met = Take(AgentJson.StringArray(o, "met"), known, claimed);
        var conflicts = Take(AgentJson.StringArray(o, "conflicts"), known, claimed);
        var missing = Take(AgentJson.StringArray(o, "missing"), known, claimed);

        missing.AddRange(known.Except(claimed)); // anything unclassified is a gap

        var summary = AgentJson.String(o, "summary");
        if (summary.Length == 0)
            summary = $"{met.Count}/{criteria.Items.Count} criteria documented.";

        return new EvidenceGapAssessment(met, missing, conflicts, summary);
    }

    /// <summary>appeal-builder reply → a sanitised <see cref="AppealRecommendation"/>.</summary>
    public static AppealRecommendation ParseAppeal(
        string reply, IReadOnlyList<Precedent> shortlist, bool denied)
    {
        var ids = shortlist.Select(p => p.CaseId).ToHashSet();
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

    private static List<string> Take(IEnumerable<string> ids, HashSet<string> known, HashSet<string> claimed) =>
        ids.Where(id => known.Contains(id) && claimed.Add(id)).ToList();
}
