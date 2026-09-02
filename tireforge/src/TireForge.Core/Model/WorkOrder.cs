namespace TireForge.Core.Model;

/// <summary>
/// A maintenance work order. The <b>only</b> rows in this table are written by the
/// Work Order Adapter (invariant 1.1). Rejections are written here too, as audit
/// rows with <see cref="WorkOrderStatus.Rejected"/> (Build Plan §15, Stage K).
/// </summary>
public class WorkOrder
{
    /// <summary>Work order id: <c>WO-&lt;ticks&gt;-&lt;rand&gt;</c>.</summary>
    public required string Id { get; set; }

    public required string DiagnosisId { get; set; }
    public Diagnosis? Diagnosis { get; set; }

    public required string MachineId { get; set; }

    public required string Fault { get; set; }
    public Severity Severity { get; set; }

    /// <summary>Source reading the work order cites (invariant 1.2).</summary>
    public required string ReadingId { get; set; }

    public string ActionText { get; set; } = "";

    public WorkOrderStatus Status { get; set; }

    /// <summary>Who issued it: <c>system</c> (auto route) or a reviewer id.</summary>
    public string IssuedBy { get; set; } = "system";

    /// <summary>Set when <see cref="Status"/> is <see cref="WorkOrderStatus.Rejected"/>.</summary>
    public string? RejectNote { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
}
