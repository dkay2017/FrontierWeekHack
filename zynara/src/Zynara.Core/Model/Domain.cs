namespace Zynara.Core.Model;

/// <summary>Payer-agnostic region. Flips the criteria set, terminology and appeal route.</summary>
public enum Region
{
    UK,
    US,
}

/// <summary>Recorded outcome of a past submission — the fact the design is built on.</summary>
public enum AppealOutcome
{
    NotAppealed,
    AppealWon,
    AppealLost,
    Pending,
}

/// <summary>The clinician request — the only input to the system (Architecture §1 / TDD §6 Intake).</summary>
public sealed record Request
{
    public required string Id { get; init; }

    /// <summary>Procedure / service requested — a CCSD (UK) or CPT/HCPCS (US) code or its name.</summary>
    public required string Procedure { get; init; }

    /// <summary>Payer + plan, e.g. "Bupa/Comprehensive" or "Aetna/PPO".</summary>
    public required string PayerPlan { get; init; }

    public required Region Region { get; init; }

    /// <summary>Free-text clinical note (an uploaded file in production; a string here).</summary>
    public required string ClinicalNote { get; init; }

    /// <summary>The denial letter text — present only once a denial has occurred (drives the appeal path).</summary>
    public string? DenialLetter { get; init; }

    /// <summary>Estimated claim value; the Gate compares this against the auto-submit limit.</summary>
    public decimal? EstimatedValue { get; init; }
}

/// <summary>One entry in a payer's rule-set: does this plan require prior auth for this procedure.</summary>
public sealed record PolicyRule(
    string PayerPlan,
    Region Region,
    string Procedure,
    bool AuthRequired,
    string RequiredCode,
    string PolicyRef,
    string? Notes = null);

/// <summary>
/// One written approval criterion for a procedure. A <see cref="Mandatory"/>
/// criterion is a hard gate — it can never be averaged away by supporting
/// criteria (evaluator finding #3).
/// </summary>
public sealed record Criterion(string Id, string Text, bool Mandatory = false);

/// <summary>A procedure's written approval criteria for one payer, at one policy version.</summary>
public sealed record Criteria(
    string PayerPlan,
    string Procedure,
    string PolicyRef,
    string Version,
    IReadOnlyList<Criterion> Items);

/// <summary>
/// A past submission and its recorded outcome. <see cref="FactPattern"/> is the narrative
/// the appeal-builder agent reasons over (File Search); the rest is structured metadata
/// that <c>AppealMatch</c> filters and ranks deterministically.
/// </summary>
public sealed record Precedent(
    string CaseId,
    string PayerPlan,
    string Procedure,
    Region Region,
    string FactPattern,
    bool InitiallyApproved,
    AppealOutcome AppealOutcome,
    IReadOnlyList<string> DenialReasonCodes,
    IReadOnlyList<string> ClausesCited,
    DateOnly DecidedOn);

/// <summary>An approved authorisation with a validity window — watched for expiry.</summary>
public sealed record AuthRecord(
    string AuthId,
    string RequestId,
    string PayerPlan,
    string Procedure,
    DateOnly ApprovedOn,
    DateOnly ValidUntil);

/// <summary>One version of a payer policy document — compared version-over-version for drift.</summary>
public sealed record PolicyVersion(
    string PolicyRef,
    string Version,
    DateOnly EffectiveFrom,
    IReadOnlyList<Criterion> Criteria);
