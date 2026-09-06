using Zynara.Core.Model;

namespace Zynara.Core.Profiles;

/// <summary>
/// The built-in Policy + Regulatory Profiles. Ships in code so the pipeline runs
/// with no config; a deployment overrides them from <c>config/profiles/*.json</c>
/// (same shape). Deliberately kept as reference data — <b>not</b> a product
/// centrepiece (review guardrail §18).
/// </summary>
public static class RegulatoryProfiles
{
    public static readonly RegulatoryProfile Uk = new(
        Id: "uk",
        Region: Region.UK,
        Name: "United Kingdom — private medical insurance",
        ShortLabel: "UK · PMI",
        Currency: "GBP",
        CurrencySymbol: "£",
        Terminology: new Dictionary<string, string>
        {
            ["prior-auth"] = "prior authorisation",
            ["clinician"] = "clinician / consultant",
            ["payer"] = "insurer",
            ["member"] = "member",
            ["procedure-code"] = "CCSD code",
        },
        CodingConventions: "CCSD procedure codes · ICD-10 diagnosis",
        Regulators: new[]
        {
            "Financial Conduct Authority (FCA)",
            "Financial Ombudsman Service (FOS)",
            "UK GDPR / Data Protection Act 2018",
        },
        AppealPath: new[]
        {
            "Payer internal reconsideration",
            "Financial Ombudsman Service (FOS)",
            "Courts",
        },
        IntegrationProfile: "No mandated prior-auth API — payer portals + secure messaging + PDF. FHIR UK Core emerging.",
        PolicyPack: "Per-payer plan rule-sets (Bupa / AXA Health / Vitality), versioned per procedure in Cosmos.");

    public static readonly RegulatoryProfile Us = new(
        Id: "us",
        Region: Region.US,
        Name: "United States — commercial health plan",
        ShortLabel: "US · Commercial",
        Currency: "USD",
        CurrencySymbol: "$",
        Terminology: new Dictionary<string, string>
        {
            ["prior-auth"] = "prior authorization",
            ["clinician"] = "provider",
            ["payer"] = "health plan",
            ["member"] = "member",
            ["procedure-code"] = "CPT / HCPCS code",
        },
        CodingConventions: "CPT / HCPCS procedure codes · ICD-10-CM diagnosis",
        Regulators: new[]
        {
            "CMS — Interoperability & Prior Authorization Final Rule (CMS-0057-F)",
            "ERISA (self-funded plans)",
            "HIPAA",
            "State Departments of Insurance",
        },
        AppealPath: new[]
        {
            "Payer internal appeal",
            "External review by an Independent Review Organization (IRO)",
            "State DOI / federal (ERISA) review",
        },
        IntegrationProfile: "X12 278 today; CMS-0057-F mandates the FHIR Prior Auth API (Da Vinci PAS / CRD / DTR) from Jan 2027.",
        PolicyPack: "Payer medical policies + InterQual / MCG criteria, versioned per procedure in Cosmos.");

    public static IReadOnlyList<RegulatoryProfile> All { get; } = new[] { Uk, Us };

    public static RegulatoryProfile For(Region region) =>
        All.FirstOrDefault(p => p.Region == region)
        ?? throw new ArgumentOutOfRangeException(nameof(region), region, "No profile for this region.");
}
