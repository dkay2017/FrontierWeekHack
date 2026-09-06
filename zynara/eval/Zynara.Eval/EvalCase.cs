using System.Text.Json;
using System.Text.Json.Serialization;
using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Eval;

/// <summary>
/// One labelled clinical case — the request, its world (criteria + rules +
/// precedents), and the expert ground truth the metrics are scored against.
/// </summary>
public sealed record EvalCase
{
    public required string Id { get; init; }
    public string Description { get; init; } = "";

    public required EvalRequest Request { get; init; }
    public required IReadOnlyList<EvalCriterion> Criteria { get; init; }
    public IReadOnlyList<EvalRule> Rules { get; init; } = Array.Empty<EvalRule>();
    public IReadOnlyList<EvalPrecedent> Precedents { get; init; } = Array.Empty<EvalPrecedent>();
    public required EvalGroundTruth GroundTruth { get; init; }

    // --- projections into the domain -----------------------------------------

    public Request ToRequest() => new()
    {
        Id = Id,
        Procedure = Request.Procedure,
        PayerPlan = Request.PayerPlan,
        Region = Enum.Parse<Region>(Request.Region, ignoreCase: true),
        ClinicalNote = Request.ClinicalNote,
        DenialLetter = Request.DenialLetter,
        EstimatedValue = Request.EstimatedValue,
    };

    public Criteria ToCriteria()
    {
        var region = Enum.Parse<Region>(Request.Region, ignoreCase: true);
        return new Criteria(
            Request.PayerPlan, Request.Procedure,
            Rules.FirstOrDefault()?.PolicyRef ?? "policy", "v1",
            Criteria.Select(c => new Criterion(c.Id, c.Text, c.Mandatory)).ToList());
    }

    public InMemoryZynaraStore ToStore()
    {
        var region = Enum.Parse<Region>(Request.Region, ignoreCase: true);
        var store = new InMemoryZynaraStore().AddCriteria(ToCriteria());

        foreach (var r in Rules)
            store.AddRule(new PolicyRule(
                Request.PayerPlan, region, Request.Procedure,
                r.AuthRequired, r.RequiredCode, r.PolicyRef, r.Notes));

        if (Rules.Count == 0)
            store.AddRule(new PolicyRule(
                Request.PayerPlan, region, Request.Procedure, true, "code", "policy"));

        foreach (var p in Precedents)
            store.AddPrecedent(new Precedent(
                p.CaseId, Request.PayerPlan, Request.Procedure, region, p.FactPattern,
                InitiallyApproved: p.AppealOutcome == "NotAppealed",
                AppealOutcome: Enum.Parse<AppealOutcome>(p.AppealOutcome, ignoreCase: true),
                DenialReasonCodes: p.DenialReasonCodes,
                ClausesCited: p.ClausesCited,
                DecidedOn: DateOnly.Parse(p.DecidedOn)));

        return store;
    }

    // --- loading -------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        Converters = { new JsonStringEnumConverter() },
    };

    public static IReadOnlyList<EvalCase> LoadAll(string? directory = null)
    {
        directory ??= Path.Combine(AppContext.BaseDirectory, "cases");
        if (!Directory.Exists(directory))
            throw new DirectoryNotFoundException($"No eval cases at '{directory}'.");

        return Directory.EnumerateFiles(directory, "*.json")
            .OrderBy(f => f)
            .Select(f => JsonSerializer.Deserialize<EvalCase>(File.ReadAllText(f), JsonOpts)
                         ?? throw new InvalidOperationException($"Could not parse eval case '{f}'."))
            .ToList();
    }
}

public sealed record EvalRequest
{
    public required string Procedure { get; init; }
    public required string PayerPlan { get; init; }
    public required string Region { get; init; }
    public required string ClinicalNote { get; init; }
    public string? DenialLetter { get; init; }
    public decimal? EstimatedValue { get; init; }
}

public sealed record EvalCriterion
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public bool Mandatory { get; init; }
}

public sealed record EvalRule
{
    public bool AuthRequired { get; init; } = true;
    public string RequiredCode { get; init; } = "code";
    public string PolicyRef { get; init; } = "policy";
    public string? Notes { get; init; }
}

public sealed record EvalPrecedent
{
    public required string CaseId { get; init; }
    public required string FactPattern { get; init; }
    public string AppealOutcome { get; init; } = "NotAppealed";
    public IReadOnlyList<string> DenialReasonCodes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ClausesCited { get; init; } = Array.Empty<string>();
    public string DecidedOn { get; init; } = "2026-01-01";
}

public sealed record EvalGroundTruth
{
    /// <summary>Expected status per criterion id: Documented / Partial / Missing / Contradicted.</summary>
    public Dictionary<string, string> CriterionStatus { get; init; } = new();

    /// <summary>Expected Gate route: AutoSubmit / Strengthen / HumanReview / Abstain.</summary>
    public required string ExpectedRoute { get; init; }

    /// <summary>Precedent ids the appeal should cite (empty for a non-appeal case).</summary>
    public IReadOnlyList<string> ExpectedCitedPrecedents { get; init; } = Array.Empty<string>();

    /// <summary>Expected precedent-strategist verdict: Submit / Strengthen / Appeal. Null = not scored.</summary>
    public string? ExpectedStrategyVerdict { get; init; }

    /// <summary>The governing policy the assembled draft must cite. Null → the case's first rule's PolicyRef.</summary>
    public string? ExpectedPolicyRef { get; init; }

    /// <summary>True when an expert would decline to advise on this case.</summary>
    public bool ExpertAbstains { get; init; }

    public GateRoute Route => Enum.Parse<GateRoute>(ExpectedRoute, ignoreCase: true);

    public StrategyVerdict? StrategyVerdict =>
        ExpectedStrategyVerdict is { } v ? Enum.Parse<StrategyVerdict>(v, ignoreCase: true) : null;

    public CriterionStatus StatusOf(string id) =>
        Enum.Parse<CriterionStatus>(CriterionStatus.GetValueOrDefault(id, "Missing"), ignoreCase: true);
}
