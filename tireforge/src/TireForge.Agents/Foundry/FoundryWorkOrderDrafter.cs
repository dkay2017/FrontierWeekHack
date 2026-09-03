using System.Diagnostics;
using TireForge.Core.Agents;
using TireForge.Core.Model;

namespace TireForge.Agents.Foundry;

/// <summary>
/// A3 backed by the hosted <c>work-order-agent</c> (Stage M, superset — D9).
/// The agent writes the crew instruction; every structured field is copied from
/// the <see cref="Diagnosis"/> (Decision D12). The reading citation is enforced.
/// </summary>
public sealed class FoundryWorkOrderDrafter(
    FoundryAgentClient client, FoundryAgentOptions options, IAgentCallRecorder recorder) : IWorkOrderDrafter
{
    public async Task<WorkOrderDraft> DraftAsync(Diagnosis diagnosis, Machine machine, CancellationToken ct = default)
    {
        var prompt =
            $"Diagnosis {diagnosis.Id}: machine {machine.Id} ({machine.Name}); " +
            $"fault \"{diagnosis.Fault}\"; severity {diagnosis.Severity}; " +
            $"confidence {diagnosis.Confidence:0.00}; triggered by reading {diagnosis.ReadingId}. " +
            "Write the work-order instruction.";

        var inv = await client.InvokeAsync(options.WorkOrderAgentName, prompt, toolHandler: null, ct);

        await recorder.RecordAsync(new AgentCallUsage(
            options.WorkOrderAgentName, options.Model, inv.InputTokens, inv.OutputTokens,
            inv.ToolCalls, diagnosis.ReadingId, diagnosis.TraceId ?? Activity.Current?.TraceId.ToString()), ct);

        var action = inv.Text.Trim();
        if (action.Length == 0)
            action = $"Attend to {diagnosis.Fault} on {machine.Id} ({machine.Name}).";
        if (!action.Contains(diagnosis.ReadingId, StringComparison.Ordinal))
            action += $" Triggered by reading {diagnosis.ReadingId}.";

        return new WorkOrderDraft(
            MachineId: machine.Id,
            Fault: diagnosis.Fault,
            Severity: diagnosis.Severity,
            ReadingId: diagnosis.ReadingId,
            ActionText: action);
    }
}
