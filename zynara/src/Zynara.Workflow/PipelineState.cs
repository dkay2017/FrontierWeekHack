using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Workflow;

/// <summary>
/// The accumulator threaded down the workflow's sequential spine. Each executor
/// returns <c>state with { … }</c> adding its piece; the Gate executor reads the
/// whole thing. Immutable — MAF checkpoints it between supersteps.
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
