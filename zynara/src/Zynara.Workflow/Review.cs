using Zynara.Core.Authority;

namespace Zynara.Workflow;

/// <summary>
/// What the workflow hands to the reviewer at the <c>review</c> port. Small on
/// purpose — the full case is in the <c>cases</c> store, read by request id.
/// </summary>
public sealed record ReviewCard(string RequestId, string Procedure, string PayerPlan, string Route, string Headline);

/// <summary>The reviewer's answer, posted back to <c>/respond/{runId}</c>.</summary>
public sealed record ReviewDecision(string Action, string? By, ReviewerRole Role, string? Note);
