using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>critic</c> backed by the hosted agent — the second reasoning agent in the
/// loop. It challenges the assembled recommendation and can force a human or an
/// abstention. Verdict + flags are sanitised; a non-parseable reply blocks (safe
/// default — a human sees it).
/// </summary>
public sealed class FoundryCriticAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : ICriticAgent
{
    public async Task<CriticReview> ReviewAsync(CriticContext c, CancellationToken ct = default)
    {
        var prompt = BuildPrompt(c);
        var inv = await client.InvokeAsync(options.CriticAgentName, prompt, toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.CriticAgentName, options.Model, inv, c.Request.Id, ct);
        return Parse(inv.Text);
    }

    private static string BuildPrompt(CriticContext c)
    {
        var sb = new StringBuilder()
            .AppendLine($"Case: {c.Request.Procedure} · {c.Request.PayerPlan} · {c.Request.Region}.")
            .AppendLine($"Needs-auth: {c.NeedsAuthNote}")
            .AppendLine($"Denial letter present: {(string.IsNullOrWhiteSpace(c.Request.DenialLetter) ? "no" : "yes")}")
            .AppendLine()
            .AppendLine($"Evidence ({c.Evidence.Quality} quality) — {c.Evidence.Summary}");
        foreach (var f in c.Evidence.Findings)
            sb.AppendLine($"  [{f.CriterionId}] {f.Status}{(f.Evidence is { Length: > 0 } e ? $" — \"{e}\"" : "")}");
        if (c.UnmetMandatory.Count > 0)
            sb.AppendLine($"  UNMET MANDATORY: {string.Join(", ", c.UnmetMandatory)}");

        sb.AppendLine().AppendLine("Precedents considered:");
        foreach (var p in c.Precedents)
            sb.AppendLine($"  [{p.Precedent.CaseId}] similarity {p.Similarity:0.00}, appeal {p.Precedent.AppealOutcome}");

        sb.AppendLine().AppendLine(
            $"Proposed recommendation: {c.Recommendation.Verdict} — {c.Recommendation.Text}");
        if (c.Recommendation.AppealDraft is { Length: > 0 } d)
            sb.AppendLine($"Appeal draft:\n{d}");

        sb.AppendLine().AppendLine("Now run your seven checks and return the JSON verdict.");
        return sb.ToString();
    }

    private static CriticReview Parse(string reply)
    {
        var obj = AgentJson.FirstObject(reply);
        if (obj is not { } o)
            return new CriticReview(CriticVerdict.Block, Array.Empty<CriticFlag>(),
                "Critic response could not be parsed — routed to a reviewer.");

        var verdict = AgentJson.String(o, "verdict").Trim().ToLowerInvariant() switch
        {
            "clear" => CriticVerdict.Clear,
            "concerns" => CriticVerdict.Concerns,
            "abstain" => CriticVerdict.Abstain,
            _ => CriticVerdict.Block,
        };

        var flags = new List<CriticFlag>();
        if (o.TryGetProperty("flags", out var arr) && arr.ValueKind == System.Text.Json.JsonValueKind.Array)
            foreach (var el in arr.EnumerateArray())
                flags.Add(new CriticFlag(AgentJson.String(el, "check"), AgentJson.String(el, "concern")));

        var summary = AgentJson.String(o, "summary");
        if (summary.Length == 0)
            summary = $"Critic verdict: {verdict}.";

        return new CriticReview(verdict, flags, summary);
    }
}
