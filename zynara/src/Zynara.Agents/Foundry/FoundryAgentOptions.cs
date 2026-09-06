namespace Zynara.Agents.Foundry;

/// <summary>
/// Config for the hosted Foundry agents. Bound from <c>ZYNARA_*</c> environment
/// values by <see cref="Zynara.Agents.DependencyInjection"/>.
/// </summary>
public sealed class FoundryAgentOptions
{
    /// <summary>Foundry project endpoint — <c>PROJECT_ENDPOINT</c> / <c>PROJECT_CONNECTION_STRING</c>.</summary>
    public required string ProjectEndpoint { get; init; }

    /// <summary>Model deployment — <c>MODEL_DEPLOYMENT_NAME</c> (default per TDD: GPT-5.4 class).</summary>
    public string Model { get; init; } = "gpt-5.4";

    public string NeedsAuthAgentName { get; init; } = "needs-auth-agent";
    public string EvidenceGapAgentName { get; init; } = "evidence-gap-agent";
    public string ClaimsAgentName { get; init; } = "claims-extraction-agent";
    public string AppealBuilderAgentName { get; init; } = "appeal-builder-agent";
    public string CriticAgentName { get; init; } = "critic-agent";

    /// <summary>
    /// Vector store id for File Search grounding (Blob corpus). Null until the
    /// infra provisions it — the agents then run on inline data only.
    /// </summary>
    public string? VectorStoreId { get; init; }

    public IReadOnlyList<string> AllAgentNames =>
    [
        NeedsAuthAgentName, EvidenceGapAgentName, ClaimsAgentName, AppealBuilderAgentName,
        CriticAgentName,
    ];
}
