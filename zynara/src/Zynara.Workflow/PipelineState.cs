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
/// (<see cref="CareApprovalWorkflow.BuildDurable"/>) once <c>assemble</c> has run
/// the pipeline and persisted the result to the case store. It carries only what
/// the routing edges and the later steps read — never the clinical note or the
/// denial letter. MAF auto-yields every handler's return value into the workflow
/// snapshot, which Durable Task caps at 16&nbsp;KB (UTF-16).
/// </summary>
public sealed record Flow(string RequestId, string Procedure, string PayerPlan)
{
    public bool AuthRequired { get; init; } = true;
    public GateRoute Route { get; init; } = GateRoute.HumanReview;
}
