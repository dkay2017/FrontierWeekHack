using Zynara.Core.Abstractions;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Core.Pipeline;

/// <summary>
/// Spoke 2. Pulls the payer's written criteria for the procedure, has the
/// <c>evidence-gap</c> agent read the clinical note against them, then builds the
/// evidence side of the structured decision model — <b>mandatory</b> criteria
/// tracked separately from supporting ones, never averaged into a single number
/// (evaluator finding #3).
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

        var status = assessment.Findings.ToDictionary(f => f.CriterionId, f => f.Status);
        var mandatory = criteria.Items.Where(c => c.Mandatory).Select(c => c.Id).ToList();
        var supporting = criteria.Items.Where(c => !c.Mandatory).Select(c => c.Id).ToList();

        var unmetMandatory = mandatory
            .Where(id => status.GetValueOrDefault(id) != CriterionStatus.Documented)
            .ToList();

        return new EvidenceGapResult(
            Assessment: assessment,
            MandatoryPass: unmetMandatory.Count == 0,
            MandatoryTotal: mandatory.Count,
            SupportingDocumented: supporting.Count(id => status.GetValueOrDefault(id) == CriterionStatus.Documented),
            SupportingPartial: supporting.Count(id => status.GetValueOrDefault(id) == CriterionStatus.Partial),
            SupportingMissing: supporting.Count(id => status.GetValueOrDefault(id) is CriterionStatus.Missing),
            ContradictionDetected: assessment.AnyContradiction)
        {
            UnmetMandatory = unmetMandatory,
        };
    }
}
