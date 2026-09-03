namespace TireForge.Agents.Foundry;

/// <summary>
/// Config for the real Foundry agents (Stage M). Bound from <c>TIREFORGE_*</c> /
/// <c>factory/.env</c> values by <see cref="TireForge.Agents.DependencyInjection"/>.
/// </summary>
public sealed class FoundryAgentOptions
{
    /// <summary>Project endpoint — <c>PROJECT_CONNECTION_STRING</c> in <c>factory/.env</c>.</summary>
    public required string ProjectEndpoint { get; init; }

    /// <summary>Model deployment — <c>MODEL_DEPLOYMENT_NAME</c> (D1: <c>gpt-5.4</c>).</summary>
    public string Model { get; init; } = "gpt-5.4";

    public string AnomalyAgentName { get; init; } = "anomaly-detection-agent";
    public string FaultAgentName { get; init; } = "fault-diagnosis-agent";
    public string WorkOrderAgentName { get; init; } = "work-order-agent";

    public const string ModeVariable = "TIREFORGE_AGENTS"; // "stub" (default) | "foundry"
}
