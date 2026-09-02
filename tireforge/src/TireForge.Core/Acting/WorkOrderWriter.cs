using TireForge.Core.Abstractions;
using TireForge.Core.Agents;
using TireForge.Core.Model;

namespace TireForge.Core.Acting;

/// <summary>Outcome of the Act step (Build Plan Stage I).</summary>
public sealed record ActResult(GateRoute Route, DiagnosisStatus DiagnosisStatus, WorkOrder? WorkOrder)
{
    public bool WorkOrderIssued => WorkOrder is not null;
}

/// <summary>
/// The Act step — the WorkOrderWriter spoke (Build Plan Stage I). Honours the Gate
/// route: <see cref="GateRoute.Auto"/> issues a work order immediately through the
/// Work Order Adapter (<see cref="IWorkOrderStore"/>, the sole write path,
/// invariant 1.1) and marks the diagnosis <see cref="DiagnosisStatus.AutoIssued"/>;
/// <see cref="GateRoute.Review"/> leaves the diagnosis <see cref="DiagnosisStatus.Pending"/>
/// with no work order.
/// </summary>
public sealed class WorkOrderWriter(IWorkOrderStore workOrders, IDiagnosisStore diagnoses)
{
    public async Task<ActResult> ActAsync(
        Diagnosis diagnosis, WorkOrderDraft draft, DateTimeOffset at, string by = "system", CancellationToken ct = default)
    {
        if (draft.ReadingId != diagnosis.ReadingId)
            throw new ArgumentException("Work-order draft cites a different reading than the diagnosis.");

        if (diagnosis.Route == GateRoute.Review)
        {
            diagnosis.Status = DiagnosisStatus.Pending;
            await diagnoses.UpdateAsync(diagnosis, ct);
            return new ActResult(GateRoute.Review, DiagnosisStatus.Pending, null);
        }

        var workOrder = new WorkOrder
        {
            Id = Ids.WorkOrder(at),
            DiagnosisId = diagnosis.Id,
            MachineId = draft.MachineId,
            Fault = draft.Fault,
            Severity = draft.Severity,
            ReadingId = draft.ReadingId,
            ActionText = draft.ActionText,
            Status = WorkOrderStatus.Issued,
            IssuedBy = by,
            CreatedAt = at,
        };
        await workOrders.AddAsync(workOrder, ct);

        diagnosis.Status = DiagnosisStatus.AutoIssued;
        await diagnoses.UpdateAsync(diagnosis, ct);

        return new ActResult(GateRoute.Auto, DiagnosisStatus.AutoIssued, workOrder);
    }
}
