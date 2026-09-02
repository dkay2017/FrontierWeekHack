using TireForge.Core.Model;
using TireForge.Core.Thresholds;

namespace TireForge.Data.Tests;

/// <summary>Build Plan Stage L — the dashboard read models.</summary>
public class ReportsTests
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
    private static Reading Normal() => Reading("EX-002", 115, 12.5, 2.1, 30);

    // --- /status --------------------------------------------------------

    [Fact]
    public async Task Status_lists_every_machine_with_bands_and_the_latest_reading()
    {
        using var h = await PipelineHarness.CreateAsync();
        var status = await h.NewReports().StatusAsync();

        Assert.Equal(5, status.Machines.Count);
        var press = status.Machines.Single(m => m.Id == "CP-003");
        Assert.Equal("curing_press", press.Name);
        Assert.Equal(198.5, press.Temperature.Value);     // from the seeded snapshot reading
        Assert.Equal(3.0, press.Vibration.Max);
        Assert.Equal(SensorStatus.High, press.Vibration.Standing); // 7.3 > 3.0
        Assert.Equal(Severity.Crit, press.Status);
    }

    [Fact]
    public async Task Status_reflects_a_run_that_flagged_an_anomaly_in_the_last_24h()
    {
        using var h = await PipelineHarness.CreateAsync();
        await h.NewPipeline().RunAsync(Crit());

        var status = await h.NewReports().StatusAsync();
        Assert.Equal(1, status.Machines.Single(m => m.Id == "CP-003").Anomalies24h);
        Assert.Equal(0, status.Machines.Single(m => m.Id == "EX-002").Anomalies24h);
    }

    // --- /queue --------------------------------------------------------

    [Fact]
    public async Task Queue_returns_pending_diagnoses_with_the_full_trace()
    {
        using var h = await PipelineHarness.CreateAsync();
        var run = await h.NewPipeline().RunAsync(Crit());

        var queue = await h.NewReports().QueueAsync();
        var item = Assert.Single(queue.Items);
        Assert.Equal(run.Diagnosis!.Id, item.Id);
        Assert.Equal("curing_press", item.MachineName);
        Assert.Equal(GateRoute.Review, item.Route);
        Assert.StartsWith("A1 ", item.DetectText);
        Assert.StartsWith("T2 ", item.MatchText);
        Assert.StartsWith("A2 ", item.DiagnoseText);
        Assert.Contains("inc-005", item.IncidentCites);
        Assert.False(string.IsNullOrWhiteSpace(item.DraftActionText));
        Assert.Equal(run.TraceId, item.TraceId);
    }

    [Fact]
    public async Task Queue_excludes_auto_issued_diagnoses()
    {
        using var h = await PipelineHarness.CreateAsync();
        await h.NewPipeline().RunAsync(ConfidentWarn()); // auto route

        Assert.Empty((await h.NewReports().QueueAsync()).Items);
    }

    // --- /workorders --------------------------------------------------

    [Fact]
    public async Task WorkOrders_shows_lifecycle_and_maps_the_issuer()
    {
        using var h = await PipelineHarness.CreateAsync();
        await h.NewPipeline().RunAsync(ConfidentWarn());         // auto WO (by = system)
        var review = await h.NewPipeline().RunAsync(Crit());
        await h.NewReviewer().ApproveAsync(review.Diagnosis!.Id, "alice");

        var wos = (await h.NewReports().WorkOrdersAsync()).Items;
        Assert.Equal(2, wos.Count);
        Assert.Contains(wos, w => w.By == "auto" && w.Status == WorkOrderStatus.Issued);
        Assert.Contains(wos, w => w.By == "reviewer" && w.Status == WorkOrderStatus.Approved);
    }

    // --- /health -----------------------------------------------------

    [Fact]
    public async Task Health_counts_in_spec_machines_open_closed_and_resolution()
    {
        using var h = await PipelineHarness.CreateAsync();
        await h.NewPipeline().RunAsync(ConfidentWarn());   // -> auto WO (open)
        var review = await h.NewPipeline().RunAsync(Crit());
        var wo = await h.NewReviewer().ApproveAsync(review.Diagnosis!.Id, "alice");
        await h.NewReviewer().CloseAsync(wo.Id);           // 1 closed

        var health = await h.NewReports().HealthAsync();
        Assert.Equal(5, health.MachineCount);
        Assert.Equal(1, health.WorkOrdersOpen);
        Assert.Equal(1, health.WorkOrdersClosed);
        Assert.Equal(0.5, health.ResolutionRate);         // 1 closed of 2 decided
        Assert.True(health.Anomalies24h >= 2);
        Assert.Equal(health.Anomalies24h, health.AnomaliesByMachine24h.Values.Sum());
    }

    [Fact]
    public async Task Health_on_a_fresh_seed_has_every_machine_in_spec()
    {
        using var h = await PipelineHarness.CreateAsync();
        var health = await h.NewReports().HealthAsync();

        // The seeded snapshot readings put MX-001/IS-005 at Warn and CP-003 at Crit;
        // EX-002 and CU-004 are in spec.
        Assert.Equal(2, health.MachinesInSpec);
        Assert.Equal(0, health.WorkOrdersOpen);
        Assert.Equal(0, health.ResolutionRate);
    }

    // --- /cost -----------------------------------------------------

    [Fact]
    public async Task Cost_reports_call_counts_and_flags_token_metrics_as_unavailable()
    {
        using var h = await PipelineHarness.CreateAsync();
        await h.NewPipeline().RunAsync(Crit());
        await h.NewPipeline().RunAsync(Normal());

        var cost = await h.NewReports().CostAsync();
        Assert.False(cost.TokenMetricsAvailable);
        Assert.All(cost.Agents, a => Assert.Equal("gpt-5.4", a.Model));
        Assert.All(cost.Agents, a => Assert.Null(a.Tokens));

        var diagnosis = cost.Agents.Single(a => a.Agent == "Fault Diagnosis");
        Assert.Equal(1, diagnosis.Calls); // only the anomalous run produced a diagnosis
    }
}
