using TireForge.Core.Model;
using TireForge.Core.Reviewing;

namespace TireForge.Data.Tests;

/// <summary>Build Plan Stage K — reviewer approve / reject / close.</summary>
public class ReviewerTests
{
    private static Reading Reading(string machineId, double t, double p, double v, double r) => new()
    {
        Id = Ids.Reading(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)),
        MachineId = machineId,
        CapturedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };

    private static Reading Crit() => Reading("CP-003", 198.5, 18.2, 7.3, 0);
    private static Reading ConfidentWarn() => Reading("IS-005", 24, 1.0, 5.2, 1800);

    private static Reviewer NewReviewer(PipelineHarness h) => h.NewReviewer();

    // --- Approve -----------------------------------------------------------

    [Fact]
    public async Task Approve_issues_the_drafted_work_order_credited_to_the_reviewer()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());
        var draft = run.Diagnosis!.DraftActionText;

        var wo = await NewReviewer(h).ApproveAsync(run.Diagnosis.Id, "alice");

        Assert.Equal(WorkOrderStatus.Approved, wo.Status);
        Assert.Equal("alice", wo.IssuedBy);
        Assert.Equal(draft, wo.ActionText);
        Assert.Equal(run.Diagnosis.ReadingId, wo.ReadingId);

        Assert.Equal(DiagnosisStatus.Approved, (await h.Diagnoses().GetAsync(run.Diagnosis.Id))!.Status);
        Assert.Empty(await h.Diagnoses().PendingAsync());
        Assert.Single(await h.WorkOrders().ListAsync());
    }

    [Fact]
    public async Task Approving_a_non_pending_diagnosis_is_rejected()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());
        await NewReviewer(h).ApproveAsync(run.Diagnosis!.Id, "alice");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewReviewer(h).ApproveAsync(run.Diagnosis.Id, "bob"));
    }

    [Fact]
    public async Task Approving_an_unknown_diagnosis_is_rejected()
    {
        using var h = await PipelineHarness.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => NewReviewer(h).ApproveAsync("dx-nope", "alice"));
    }

    // --- Reject ----------------------------------------------------------

    [Fact]
    public async Task Reject_writes_an_audit_row_and_no_active_work_order()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());

        var audit = await NewReviewer(h).RejectAsync(run.Diagnosis!.Id, "bob", "sensor recalibrated, false alarm");

        Assert.Equal(WorkOrderStatus.Rejected, audit.Status);
        Assert.Equal("bob", audit.IssuedBy);
        Assert.Equal("sensor recalibrated, false alarm", audit.RejectNote);

        Assert.Equal(DiagnosisStatus.Rejected, (await h.Diagnoses().GetAsync(run.Diagnosis.Id))!.Status);
        var all = await h.WorkOrders().ListAsync();
        Assert.Single(all);
        Assert.DoesNotContain(all, w => w.Status is WorkOrderStatus.Issued or WorkOrderStatus.Approved);
    }

    [Fact]
    public async Task Reject_requires_a_note()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());

        await Assert.ThrowsAsync<ArgumentException>(
            () => NewReviewer(h).RejectAsync(run.Diagnosis!.Id, "bob", "   "));
    }

    // --- Close ---------------------------------------------------------

    [Fact]
    public async Task Close_moves_an_approved_work_order_to_closed()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());
        var wo = await NewReviewer(h).ApproveAsync(run.Diagnosis!.Id, "alice");

        var closed = await NewReviewer(h).CloseAsync(wo.Id);

        Assert.Equal(WorkOrderStatus.Closed, closed.Status);
        Assert.NotNull(closed.ClosedAt);
    }

    [Fact]
    public async Task Close_moves_an_auto_issued_work_order_to_closed()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(ConfidentWarn());
        var issued = Assert.Single(await h.WorkOrders().ListAsync());
        Assert.Equal(WorkOrderStatus.Issued, issued.Status);

        var closed = await NewReviewer(h).CloseAsync(issued.Id);
        Assert.Equal(WorkOrderStatus.Closed, closed.Status);
    }

    [Fact]
    public async Task Closing_a_rejected_work_order_is_refused()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());
        var audit = await NewReviewer(h).RejectAsync(run.Diagnosis!.Id, "bob", "false alarm");

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewReviewer(h).CloseAsync(audit.Id));
    }

    [Fact]
    public async Task Closing_twice_is_refused()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());
        var wo = await NewReviewer(h).ApproveAsync(run.Diagnosis!.Id, "alice");
        await NewReviewer(h).CloseAsync(wo.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewReviewer(h).CloseAsync(wo.Id));
    }

    [Fact]
    public async Task Closing_an_unknown_work_order_is_refused()
    {
        using var h = await PipelineHarness.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => NewReviewer(h).CloseAsync("WO-nope"));
    }
}
