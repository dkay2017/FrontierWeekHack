using System.Diagnostics;
using TireForge.Core.Abstractions;
using TireForge.Core.Model;
using TireForge.Core.Observability;

namespace TireForge.Core.Reviewing;

/// <summary>
/// The human-in-the-loop decisions on diagnoses the Gate routed to review
/// (Build Plan Stage K). Every write still goes through the Work Order Adapter
/// (<see cref="IWorkOrderStore"/>, invariant 1.1) — approvals and rejections
/// alike. A rejection is recorded as a <see cref="WorkOrderStatus.Rejected"/>
/// audit row, never a silent drop (Build Plan §15).
/// </summary>
public sealed class Reviewer(IDiagnosisStore diagnoses, IWorkOrderStore workOrders, TimeProvider? clock = null)
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    /// <summary>Approve a pending diagnosis: issue its drafted work order, credited to the reviewer.</summary>
    public async Task<WorkOrder> ApproveAsync(string diagnosisId, string reviewer, CancellationToken ct = default)
    {
        using var act = Telemetry.Source.StartActivity("review.approve");
        var dx = await RequirePending(diagnosisId, ct);
        act?.SetTag(Telemetry.Tags.DiagnosisId, dx.Id);
        act?.SetTag(Telemetry.Tags.MachineId, dx.MachineId);

        var at = _clock.GetUtcNow();
        var workOrder = WorkOrderFrom(dx, at);
        workOrder.Status = WorkOrderStatus.Approved;
        workOrder.IssuedBy = reviewer;
        await workOrders.AddAsync(workOrder, ct);

        dx.Status = DiagnosisStatus.Approved;
        await diagnoses.UpdateAsync(dx, ct);

        act?.SetTag(Telemetry.Tags.WorkOrderId, workOrder.Id);
        return workOrder;
    }

    /// <summary>Reject a pending diagnosis: write a rejected audit row, no active work order.</summary>
    public async Task<WorkOrder> RejectAsync(
        string diagnosisId, string reviewer, string note, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(note))
            throw new ArgumentException("A rejection must carry a note.", nameof(note));

        using var act = Telemetry.Source.StartActivity("review.reject");
        var dx = await RequirePending(diagnosisId, ct);
        act?.SetTag(Telemetry.Tags.DiagnosisId, dx.Id);

        var at = _clock.GetUtcNow();
        var audit = WorkOrderFrom(dx, at);
        audit.Status = WorkOrderStatus.Rejected;
        audit.IssuedBy = reviewer;
        audit.RejectNote = note;
        await workOrders.AddAsync(audit, ct);

        dx.Status = DiagnosisStatus.Rejected;
        await diagnoses.UpdateAsync(dx, ct);

        return audit;
    }

    /// <summary>Close a work order — only from <see cref="WorkOrderStatus.Issued"/> or <see cref="WorkOrderStatus.Approved"/>.</summary>
    public async Task<WorkOrder> CloseAsync(string workOrderId, CancellationToken ct = default)
    {
        using var act = Telemetry.Source.StartActivity("review.close");
        var wo = await workOrders.GetAsync(workOrderId, ct)
            ?? throw new InvalidOperationException($"Unknown work order '{workOrderId}'.");
        act?.SetTag(Telemetry.Tags.WorkOrderId, wo.Id);

        if (wo.Status is not (WorkOrderStatus.Issued or WorkOrderStatus.Approved))
            throw new InvalidOperationException($"Cannot close a work order in state '{wo.Status}'.");

        wo.Status = WorkOrderStatus.Closed;
        wo.ClosedAt = _clock.GetUtcNow();
        await workOrders.UpdateAsync(wo, ct);
        return wo;
    }

    private async Task<Diagnosis> RequirePending(string diagnosisId, CancellationToken ct)
    {
        var dx = await diagnoses.GetAsync(diagnosisId, ct)
            ?? throw new InvalidOperationException($"Unknown diagnosis '{diagnosisId}'.");
        if (dx.Status != DiagnosisStatus.Pending)
            throw new InvalidOperationException(
                $"Diagnosis '{diagnosisId}' is '{dx.Status}', not awaiting review.");
        return dx;
    }

    private static WorkOrder WorkOrderFrom(Diagnosis dx, DateTimeOffset at) => new()
    {
        Id = Ids.WorkOrder(at),
        DiagnosisId = dx.Id,
        MachineId = dx.MachineId,
        Fault = dx.Fault,
        Severity = dx.Severity,
        ReadingId = dx.ReadingId,
        ActionText = dx.DraftActionText,
        Status = WorkOrderStatus.Issued,
        CreatedAt = at,
    };
}
