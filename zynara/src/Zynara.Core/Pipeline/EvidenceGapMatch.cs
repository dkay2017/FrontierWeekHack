using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 2. Pulls the payer's written criteria for the procedure, has the
/// <c>evidence-gap</c> agent read the clinical note against them, then derives the
/// <c>readiness</c> score the Gate consumes — <c>met / total</c>, computed here,
/// never taken from the model.
/// </summary>
public sealed class EvidenceGapMatch(IZynaraStore store, IEvidenceGapAgent agent)
{
    public async Task<EvidenceGapResult> RunAsync(Request request, CancellationToken ct = default)
    {
        var criteria = await store.GetCriteriaAsync(request.PayerPlan, request.Procedure, request.Region, ct)
            ?? throw new InvalidOperationException(
                $"No published criteria for {request.PayerPlan} / {request.Procedure} in {request.Region}.");

        var assessment = await agent.AssessAsync(request.ClinicalNote, criteria, ct);
        assessment.Validate(criteria);

        var readiness = criteria.Items.Count == 0
            ? 1.0
            : Math.Round((double)assessment.Met.Count / criteria.Items.Count, 4);

        return new EvidenceGapResult(assessment, readiness);
    }
}
