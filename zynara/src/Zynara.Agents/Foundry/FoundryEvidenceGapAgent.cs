using System.Text;
using Zynara.Core.Agents;
using Zynara.Core.Model;

namespace Zynara.Agents.Foundry;

/// <summary>
/// <c>evidence-gap</c> backed by the hosted agent. The agent reads the clinical
/// note against the numbered criteria and returns which ids are met / missing /
/// contradicted; <c>EvidenceGapMatch</c> derives the readiness score. The response
/// is sanitised against the real criterion ids, so the result always validates.
/// </summary>
public sealed class FoundryEvidenceGapAgent(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IEvidenceGapAgent
{
    public async Task<EvidenceGapAssessment> AssessAsync(
        string clinicalNote, Criteria criteria, CancellationToken ct = default)
    {
        var prompt = new StringBuilder()
            .AppendLine($"Procedure: {criteria.Procedure} · payer {criteria.PayerPlan} · policy {criteria.PolicyRef}.")
            .AppendLine("Approval criteria:");
        foreach (var c in criteria.Items)
            prompt.AppendLine($"  [{c.Id}] {c.Text}");
        prompt.AppendLine().AppendLine("Clinical note:").AppendLine(clinicalNote);

        var inv = await client.InvokeAsync(options.EvidenceGapAgentName, prompt.ToString(), toolHandler: null, ct);
        await Usage.RecordAsync(recorder, options.EvidenceGapAgentName, options.Model, inv, requestId: null, ct);

        return FoundryResponse.ParseGap(inv.Text, criteria);
    }
}
