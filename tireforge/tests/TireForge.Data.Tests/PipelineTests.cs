using TireForge.Core.Model;

namespace TireForge.Data.Tests;

/// <summary>
/// Build Plan Stage J — the composed pipeline, first end-to-end run.
/// normal → stops at D · warn (confident) → auto WO · crit → pending, no WO.
/// </summary>
public class PipelineTests
{
    private static Reading Reading(string machineId, double t, double p, double v, double r) => new()
    {
        Id = Ids.Reading(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)),
        MachineId = machineId,
        CapturedAt = new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero),
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };

    [Fact]
    public async Task Normal_reading_stops_at_detection_with_no_diagnosis_or_work_order()
    {
        using var h = await PipelineHarness.CreateAsync();
        var reading = Reading("EX-002", 115, 12.5, 2.1, 30); // all in band

        var result = await h.NewPipeline().RunAsync(reading);

        Assert.True(result.StoppedAtDetection);
        Assert.False(result.IsAnomaly);
        Assert.Null(result.Diagnosis);
        Assert.Null(result.WorkOrder);
        Assert.Empty(await h.Diagnoses().PendingAsync());
        Assert.Empty(await h.WorkOrders().ListAsync());

        var persisted = await h.Readings().GetAsync(reading.Id);
        Assert.False(persisted!.IsAnomaly);
    }

    [Fact]
    public async Task Confident_warning_auto_issues_a_work_order()
    {
        using var h = await PipelineHarness.CreateAsync();
        // IS-005 vibration 5.2 > 4.0 (single breach, 30%) → Warn; exact history inc-008 → confidence ~0.85 → Auto.
        var reading = Reading("IS-005", 24, 1.0, 5.2, 1800);

        var result = await h.NewPipeline().RunAsync(reading);

        Assert.True(result.IsAnomaly);
        Assert.NotNull(result.Diagnosis);
        Assert.Equal(GateRoute.Auto, result.Diagnosis!.Route);
        Assert.Equal(DiagnosisStatus.AutoIssued, result.Diagnosis.Status);

        var wo = Assert.Single(await h.WorkOrders().ListAsync());
        Assert.Equal(WorkOrderStatus.Issued, wo.Status);
        Assert.Equal(reading.Id, wo.ReadingId);
        Assert.Equal("spindle bearing wear", wo.Fault); // grounded in the exact prior incident
    }

    [Fact]
    public async Task Critical_reading_routes_to_review_with_no_work_order()
    {
        using var h = await PipelineHarness.CreateAsync();
        var reading = Reading("CP-003", 198.5, 18.2, 7.3, 0); // 3 breaches, vibration 143% → Crit

        var result = await h.NewPipeline().RunAsync(reading);

        Assert.Equal(Severity.Crit, result.ThresholdSeverity);
        Assert.Equal(GateRoute.Review, result.Diagnosis!.Route);
        Assert.Equal(DiagnosisStatus.Pending, result.Diagnosis.Status);
        Assert.Empty(await h.WorkOrders().ListAsync());

        var pending = Assert.Single(await h.Diagnoses().PendingAsync());
        Assert.Equal(result.Diagnosis.Id, pending.Id);
        // D7 — reviewer sees the prepared-but-unissued draft.
        Assert.False(string.IsNullOrWhiteSpace(pending.DraftActionText));
        Assert.Contains(result.ReadingId, pending.DraftActionText);
    }

    [Fact]
    public async Task One_trace_id_threads_every_step()
    {
        using var h = await PipelineHarness.CreateAsync();
        var result = await h.NewPipeline().RunAsync(Reading("CP-003", 198.5, 18.2, 7.3, 0));

        Assert.All(result.Trace, line => Assert.StartsWith($"[{result.TraceId}] ", line));
        var joined = string.Join("\n", result.Trace);
        foreach (var step in new[] { "T1 ", "A1 ", "T2 ", "A2 ", "GATE ", "ACT " })
            Assert.Contains(step, joined);

        Assert.Equal(result.TraceId, result.Diagnosis!.TraceId);
    }

    [Fact]
    public async Task Diagnosis_row_carries_the_full_trace_and_cites()
    {
        using var h = await PipelineHarness.CreateAsync();
        var result = await h.NewPipeline().RunAsync(Reading("CP-003", 198.5, 18.2, 7.3, 0));

        var dx = await h.Diagnoses().GetAsync(result.Diagnosis!.Id);
        Assert.StartsWith("A1 ", dx!.DetectText);
        Assert.StartsWith("T2 ", dx.MatchText);
        Assert.StartsWith("A2 ", dx.DiagnoseText);
        Assert.Contains("inc-005", dx.IncidentCites); // closest prior incident
    }

    [Fact]
    public async Task Unknown_machine_is_rejected()
    {
        using var h = await PipelineHarness.CreateAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            h.NewPipeline().RunAsync(Reading("ZZ-999", 1, 1, 1, 1)));
    }
}
