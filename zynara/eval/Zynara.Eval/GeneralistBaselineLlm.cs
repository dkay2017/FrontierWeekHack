using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Zynara.Core.Agents;
using Zynara.Core.Gating;
using Zynara.Core.Model;

namespace Zynara.Eval;

/// <summary>
/// The <b>credible</b> single-generalist baseline (re-eval item 1). One LLM call
/// does the entire case in one pass — needs-auth, evidence assessment, claim
/// reconciliation, precedent weighing, routing — and is scored on the same
/// metrics as the multi-agent pipeline.
///
/// CI stays offline and deterministic via <b>fixture replay</b>: the model runs
/// once (<c>dotnet run --project tools/Zynara.BaselineRefresh</c>), its verbatim
/// answers are committed under <c>baseline-fixtures/</c>, and the eval replays
/// them. A prompt change invalidates the fixtures (hash mismatch → loud failure).
///
/// See <c>BASELINE.md</c> for the design.
/// </summary>
public static class GeneralistBaselineLlm
{
    public const string AgentName = "generalist-baseline";

    public const string SystemPrompt = """
        You are a senior prior-authorisation and appeals specialist for a UK/US health
        insurer. You handle the ENTIRE case in one pass: decide whether prior
        authorisation is required, assess the clinical note against the payer's written
        criteria, extract and reconcile the clinical claims, weigh any comparable
        precedent, judge whether an appeal is warranted, and decide what should happen
        next.

        You must be safe:
        - A MANDATORY criterion that the note does not clearly satisfy is never "met".
        - If the note asserts and denies the same fact, that is a contradiction.
        - If the evidence is thin and there is no comparable precedent, decline to advise.
        - Never recommend sending anything to the payer without a human check.

        Respond with a single JSON object and nothing else:
        {
          "authRequired": true | false,
          "criteria": [ { "id": "<id>", "status": "documented|partial|missing|contradicted" } ],
          "contradictionFound": true | false,
          "citedPrecedents": [ "<precedent id>", ... ],
          "policyRef": "<the governing policy reference, copied exactly from the input, or null>",
          "route": "ReadyToSubmit" | "Strengthen" | "HumanReview" | "Abstain",
          "abstain": true | false,
          "reasoning": "<3-4 sentences>"
        }

        Route meanings:
        - ReadyToSubmit: every mandatory criterion documented, evidence solid, no
          contradiction; a human still approves before anything is sent.
        - Strengthen: mandatory criteria met but a supporting criterion is undocumented,
          or the case is over-stated relative to the evidence.
        - HumanReview: a mandatory criterion is not met, a contradiction is present, the
          value is over the auto-limit, or precedent conflicts.
        - Abstain: not enough reliable evidence and no comparable precedent to advise.

        Use only the criterion ids and precedent ids you were given. Judge only what the
        note actually says. Do not invent policy references or clauses.
        """;

    private static readonly decimal AutoLimit = new GateOptions().AutoLimit;

    public static string UserPrompt(EvalCase c)
    {
        var r = c.Request;
        var rule = c.Rules.FirstOrDefault();
        var sb = new StringBuilder();
        sb.Append("PROCEDURE: ").Append(r.Procedure).Append('\n');
        sb.Append("PAYER / PLAN: ").Append(r.PayerPlan).Append("   REGION: ").Append(r.Region).Append('\n');
        sb.Append("ESTIMATED VALUE: ").Append(r.EstimatedValue?.ToString() ?? "(not given)")
          .Append("   AUTO-LIMIT: ").Append(AutoLimit.ToString()).Append('\n');
        sb.Append('\n').Append("PAYER RULE:\n");
        sb.Append("  prior auth required: ").Append(rule?.AuthRequired ?? true).Append('\n');
        sb.Append("  procedure code: ").Append(rule?.RequiredCode ?? "(not given)").Append('\n');
        sb.Append("  governing policy: ").Append(rule?.PolicyRef ?? "(not given)").Append('\n');
        sb.Append('\n').Append("WRITTEN CRITERIA (id · mandatory · text):\n");
        foreach (var cr in c.Criteria)
            sb.Append("  ").Append(cr.Id).Append(" · ").Append(cr.Mandatory ? "MANDATORY" : "supporting")
              .Append(" · ").Append(cr.Text).Append('\n');
        sb.Append('\n').Append("CLINICAL NOTE:\n  ").Append(r.ClinicalNote.Replace("\n", "\n  ")).Append('\n');
        sb.Append('\n').Append("PRIOR DENIAL LETTER:\n  ")
          .Append(string.IsNullOrWhiteSpace(r.DenialLetter) ? "(none — this is a fresh submission)" : r.DenialLetter)
          .Append('\n');
        sb.Append('\n').Append("COMPARABLE PRECEDENT ON THE PAYER'S RECORD (id · outcome · denial codes · clauses cited · fact pattern):\n");
        if (c.Precedents.Count == 0)
            sb.Append("  (none found)\n");
        else
            foreach (var p in c.Precedents)
                sb.Append("  ").Append(p.CaseId).Append(" · ").Append(p.AppealOutcome)
                  .Append(" · ").Append(string.Join(",", p.DenialReasonCodes))
                  .Append(" · ").Append(string.Join(",", p.ClausesCited))
                  .Append(" · ").Append(p.FactPattern).Append('\n');
        return sb.ToString();
    }

    public static string PromptHash(EvalCase c)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(SystemPrompt + "\n" + UserPrompt(c)));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    // --- fixtures -----------------------------------------------------------

    public static string FixtureDir =>
        Path.Combine(AppContext.BaseDirectory, "baseline-fixtures");

    public static string FixturePath(string caseId) =>
        Path.Combine(FixtureDir, caseId + ".json");

    /// <summary>True only when every case has a fixture — the report uses the LLM baseline then.</summary>
    public static bool FixturesComplete(IReadOnlyList<EvalCase> cases) =>
        Directory.Exists(FixtureDir) && cases.All(c => File.Exists(FixturePath(c.Id)));

    /// <summary>Read + parse the committed fixture for a case. Null when absent; throws on prompt drift.</summary>
    public static BaselineVerdict? Replay(EvalCase c)
    {
        var path = FixturePath(c.Id);
        if (!File.Exists(path)) return null;

        var fx = JsonSerializer.Deserialize<Fixture>(File.ReadAllText(path), FixtureJson)
                 ?? throw new InvalidOperationException($"Baseline fixture '{path}' is not valid JSON.");

        if (fx.PromptHash != PromptHash(c))
            throw new InvalidOperationException(
                $"Baseline fixture for '{c.Id}' is stale — the prompt changed since it was captured. " +
                $"Re-run `dotnet run --project tools/Zynara.BaselineRefresh`.");

        return Parse(fx.Response)
            ?? throw new InvalidOperationException($"Baseline fixture for '{c.Id}' has an unparseable model response.");
    }

    // --- parsing ----------------------------------------------------------

    /// <summary>Pull the JSON object out of a model response and parse it. Null on failure.</summary>
    public static BaselineVerdict? Parse(string modelText)
    {
        var start = modelText.IndexOf('{');
        var end = modelText.LastIndexOf('}');
        if (start < 0 || end <= start) return null;

        Raw raw;
        try
        {
            raw = JsonSerializer.Deserialize<Raw>(modelText[start..(end + 1)], VerdictJson)!;
        }
        catch (JsonException)
        {
            return null;
        }
        if (raw is null) return null;

        var criteria = new Dictionary<string, CriterionStatus>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in raw.Criteria ?? [])
            if (!string.IsNullOrWhiteSpace(item.Id) && TryStatus(item.Status, out var st))
                criteria[item.Id] = st;

        return new BaselineVerdict(
            AuthRequired: raw.AuthRequired ?? true,
            Criteria: criteria,
            ContradictionFound: raw.ContradictionFound ?? false,
            CitedPrecedents: raw.CitedPrecedents ?? [],
            PolicyRef: string.IsNullOrWhiteSpace(raw.PolicyRef) || raw.PolicyRef == "null" ? null : raw.PolicyRef,
            Route: ParseRoute(raw.Route, raw.Abstain ?? false),
            Abstain: raw.Abstain ?? false,
            Reasoning: raw.Reasoning ?? "");
    }

    private static bool TryStatus(string? s, out CriterionStatus status)
    {
        status = CriterionStatus.Missing;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return Enum.TryParse(s.Trim(), ignoreCase: true, out status);
    }

    private static GateRoute ParseRoute(string? s, bool abstain)
    {
        if (abstain) return GateRoute.Abstain;
        var norm = Regex.Replace(s ?? "", "[^a-zA-Z]", "").ToLowerInvariant();
        return norm switch
        {
            "readytosubmit" or "autosubmit" or "ready" or "submit" => GateRoute.ReadyToSubmit,
            "strengthen" => GateRoute.Strengthen,
            "humanreview" or "review" or "human" => GateRoute.HumanReview,
            "abstain" => GateRoute.Abstain,
            _ => GateRoute.HumanReview,   // unrecognised → the safe direction
        };
    }

    private static readonly JsonSerializerOptions FixtureJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    private static readonly JsonSerializerOptions VerdictJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public sealed record Fixture(string PromptHash, string Response);

    private sealed record Raw(
        [property: JsonPropertyName("authRequired")] bool? AuthRequired,
        [property: JsonPropertyName("criteria")] List<RawCriterion>? Criteria,
        [property: JsonPropertyName("contradictionFound")] bool? ContradictionFound,
        [property: JsonPropertyName("citedPrecedents")] List<string>? CitedPrecedents,
        [property: JsonPropertyName("policyRef")] string? PolicyRef,
        [property: JsonPropertyName("route")] string? Route,
        [property: JsonPropertyName("abstain")] bool? Abstain,
        [property: JsonPropertyName("reasoning")] string? Reasoning);

    private sealed record RawCriterion(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("status")] string? Status);
}

/// <summary>The parsed one-pass verdict — the generalist's answer to the whole case.</summary>
public sealed record BaselineVerdict(
    bool AuthRequired,
    IReadOnlyDictionary<string, CriterionStatus> Criteria,
    bool ContradictionFound,
    IReadOnlyList<string> CitedPrecedents,
    string? PolicyRef,
    GateRoute Route,
    bool Abstain,
    string Reasoning)
{
    public CriterionStatus StatusOf(string id) =>
        Criteria.TryGetValue(id, out var s) ? s : CriterionStatus.Missing;
}
