using TireForge.Core.Agents;
using TireForge.Core.Model;

namespace TireForge.Agents;

/// <summary>
/// Deterministic A3 stub (Build Plan Stage H step 2): templates the work-order
/// action from the diagnosis and cites the source reading. Real Foundry agent
/// swaps in at Stage M.
/// </summary>
public sealed class StubWorkOrderDrafter : IWorkOrderDrafter
{
    public Task<WorkOrderDraft> DraftAsync(Diagnosis diagnosis, Machine machine, CancellationToken ct = default)
    {
        var urgency = diagnosis.Severity switch
        {
            Severity.Crit => "IMMEDIATE — dispatch maintenance now; stop the line if operation is unsafe",
            Severity.Warn => "WITHIN 24H — schedule maintenance",
            _ => "MONITOR — log and re-inspect next cycle",
        };

        var action =
            $"{urgency}. Machine {machine.Id} ({machine.Name}): {diagnosis.Fault}. " +
            $"Diagnosis {diagnosis.Id} (confidence {diagnosis.Confidence:0.00}). " +
            $"Triggered by reading {diagnosis.ReadingId}.";

        var draft = new WorkOrderDraft(
            MachineId: machine.Id,
            Fault: diagnosis.Fault,
            Severity: diagnosis.Severity,
            ReadingId: diagnosis.ReadingId,
            ActionText: action);

        return Task.FromResult(draft);
    }
}
