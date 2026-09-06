namespace Zynara.Data;

/// <summary>Container names + partition-key paths. Kept in one place so the seeder, the store and the Bicep agree.</summary>
public static class CosmosNames
{
    public const string Database = "careapproval";

    // reference data — curated (see TDD §2.1 · the extraction assumptions)
    public const string Criteria = "criteria";              // pk /payerPlan
    public const string PayerRules = "payerRules";           // pk /payerPlan
    public const string Precedents = "precedents";           // pk /payerPlan
    public const string PolicyVersions = "policyVersions";   // pk /policyRef
    public const string DenialCohorts = "denialCohorts";     // pk /payerPlan

    // operational state
    public const string Cases = "cases";                     // pk /requestId — the assembled case + its audit trail (D15)
    public const string Submissions = "submissions";         // pk /requestId — one outbound submission per approved case
    public const string AuthRecords = "authRecords";         // pk /requestId
    public const string EarlyWarnings = "earlyWarnings";     // pk /id
    public const string AgentCalls = "agentCalls";           // pk /day — the cost meter

    public static readonly IReadOnlyList<(string Name, string PartitionKey)> All = new[]
    {
        (Criteria, "/payerPlan"),
        (PayerRules, "/payerPlan"),
        (Precedents, "/payerPlan"),
        (PolicyVersions, "/policyRef"),
        (DenialCohorts, "/payerPlan"),
        (Cases, "/requestId"),
        (Submissions, "/requestId"),
        (AuthRecords, "/requestId"),
        (EarlyWarnings, "/id"),
        (AgentCalls, "/day"),
    };
}
