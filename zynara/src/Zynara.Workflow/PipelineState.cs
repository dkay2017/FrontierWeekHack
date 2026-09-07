using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Workflow;

/// <summary>
/// The accumulator threaded down the <see cref="CareApprovalWorkflow.Build"/>
/// (in-process) graph. Each executor returns <c>state with { … }</c> adding its
/// piece; the Gate executor reads the whole thing.
/// </summary>
public sealed record PipelineState(Request Request)
{
    public NeedsAuthResult? NeedsAuth { get; init; }
    public EvidenceGapResult? Gap { get; init; }
    public ContradictionResult Contradiction { get; init; } = ContradictionResult.None;
    public AppealMatchResult? Appeal { get; init; }
    public CriticReview? Critic { get; init; }
    public GateDecision? Decision { get; init; }

    public bool AuthRequired => NeedsAuth?.AuthRequired ?? true;
}

/// <summary>
/// The <b>small</b> message threaded down the durable graph
/// (<see cref="CareApprovalWorkflow.BuildDurable"/>). The bulky pipeline result
/// is persisted to the case store by the <c>assemble</c> step, not carried here —
/// Durable Task caps the serialised workflow snapshot at 16&nbsp;KB.
/// </summary>
public sealed record Flow(Request Request)
{
    public bool AuthRequired { get; init; } = true;
    public GateRoute Route { get; init; } = GateRoute.HumanReview;
}
