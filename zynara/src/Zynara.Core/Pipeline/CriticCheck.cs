using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 4 (evaluator P0-2). Assembles what the pipeline has concluded and hands it
/// to the <c>critic</c> agent, whose job is to try to disprove the recommendation.
/// The Critic's verdict feeds the Gate (a block → human review; abstain → the
/// system declines to advise) and is shown to the reviewer.
/// </summary>
public sealed class CriticCheck(ICriticAgent agent)
{
    public Task<CriticReview> RunAsync(
        Request request,
        NeedsAuthResult needsAuth,
        EvidenceGapResult gap,
        AppealMatchResult appeal,
        CancellationToken ct = default)
    {
        var context = new CriticContext(
            Request: request,
            NeedsAuthNote: needsAuth.Note,
            Evidence: gap.Assessment,
            UnmetMandatory: gap.UnmetMandatory,
            Precedents: appeal.Shortlist,
            Recommendation: appeal.Recommendation);

        return agent.ReviewAsync(context, ct);
    }
}
