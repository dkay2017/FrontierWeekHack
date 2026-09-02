using TireForge.Core.Acting;
using TireForge.Core.Agents;
using TireForge.Core.Model;
using TireForge.Data.Repositories;
using TireForge.Data.Seed;

namespace TireForge.Data.Tests;

/// <summary>Build Plan Stage I checks — the Act step / sole write path.</summary>
public class WorkOrderWriterTests
{
    private static async Task<TestDb> SeededWithReading()
    {
        var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);
        await new ReadingStore(db.NewContext()).AddAsync(new Reading
        {
            Id = "rdg-act-1", MachineId = "CP-003", CapturedAt = DateTimeOffset.UtcNow,
            Temperature = 198, Pressure = 18, Vibration = 7, Rpm = 0,
        });
        return db;
    }

    private static Diagnosis Dx(GateRoute route) => new()
    {
        Id = "dx-act-1", ReadingId = "rdg-act-1", MachineId = "CP-003",
        Fault = "platen bearing failure", Severity = Severity.Crit, Confidence = 0.55,
        Route = route, GateReason = "…", Status = DiagnosisStatus.Pending, CreatedAt = DateTimeOffset.UtcNow,
    };

    private static WorkOrderDraft Draft(string readingId = "rdg-act-1") =>
        new("CP-003", "platen bearing failure", Severity.Crit, readingId, "IMMEDIATE — replace platen bearings");

    [Fact]
    public async Task Auto_route_issues_a_work_order_and_marks_the_diagnosis_auto_issued()
    {
        using var db = await SeededWithReading();
        var ctx = db.NewContext();
        await new DiagnosisStore(ctx).AddAsync(Dx(GateRoute.Auto));
        var dx = await new DiagnosisStore(ctx).GetAsync("dx-act-1");

        var result = await new WorkOrderWriter(new WorkOrderStore(ctx), new DiagnosisStore(ctx))
            .ActAsync(dx!, Draft(), DateTimeOffset.UtcNow);

        Assert.True(result.WorkOrderIssued);
        Assert.Equal(WorkOrderStatus.Issued, result.WorkOrder!.Status);
        Assert.Equal("system", result.WorkOrder.IssuedBy);
        Assert.Single(await new WorkOrderStore(db.NewContext()).ListAsync());

        var stored = await new DiagnosisStore(db.NewContext()).GetAsync("dx-act-1");
        Assert.Equal(DiagnosisStatus.AutoIssued, stored!.Status);
        Assert.Equal("IMMEDIATE — replace platen bearings", stored.DraftActionText); // D7
    }

    [Fact]
    public async Task Review_route_writes_no_work_order_and_leaves_the_diagnosis_pending()
    {
        using var db = await SeededWithReading();
        var ctx = db.NewContext();
        await new DiagnosisStore(ctx).AddAsync(Dx(GateRoute.Review));
        var dx = await new DiagnosisStore(ctx).GetAsync("dx-act-1");

        var result = await new WorkOrderWriter(new WorkOrderStore(ctx), new DiagnosisStore(ctx))
            .ActAsync(dx!, Draft(), DateTimeOffset.UtcNow);

        Assert.False(result.WorkOrderIssued);
        Assert.Empty(await new WorkOrderStore(db.NewContext()).ListAsync());

        var stored = await new DiagnosisStore(db.NewContext()).GetAsync("dx-act-1");
        Assert.Equal(DiagnosisStatus.Pending, stored!.Status);
        // D7 — the draft is recorded even though no work order was issued.
        Assert.Equal("IMMEDIATE — replace platen bearings", stored.DraftActionText);
    }

    [Fact]
    public async Task Rejects_a_draft_that_cites_a_different_reading()
    {
        using var db = await SeededWithReading();
        var ctx = db.NewContext();
        await new DiagnosisStore(ctx).AddAsync(Dx(GateRoute.Auto));
        var dx = await new DiagnosisStore(ctx).GetAsync("dx-act-1");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            new WorkOrderWriter(new WorkOrderStore(ctx), new DiagnosisStore(ctx))
                .ActAsync(dx!, Draft("rdg-other"), DateTimeOffset.UtcNow));
    }
}
