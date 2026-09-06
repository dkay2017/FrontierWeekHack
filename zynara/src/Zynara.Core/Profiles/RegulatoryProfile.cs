namespace Zynara.Core.Model;

/// <summary>
/// A <b>Policy + Regulatory Profile</b> (evaluator §9). The "UK ⇄ US switch" is not
/// a document swap — a profile carries the policy pack, the terminology, the coding
/// conventions, the governing regulators, the appeal / escalation path and the
/// integration standards. Config data, curated per deployment; the pipeline reads
/// the criteria and rules keyed by <see cref="Region"/> as before.
/// </summary>
public sealed record RegulatoryProfile(
    string Id,
    Region Region,
    string Name,
    string ShortLabel,
    string Currency,
    string CurrencySymbol,
    IReadOnlyDictionary<string, string> Terminology,
    string CodingConventions,
    IReadOnlyList<string> Regulators,
    IReadOnlyList<string> AppealPath,
    string IntegrationProfile,
    string PolicyPack);
