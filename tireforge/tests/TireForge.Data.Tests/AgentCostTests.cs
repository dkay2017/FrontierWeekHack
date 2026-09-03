using TireForge.Core.Agents;
using TireForge.Core.Reporting;
using TireForge.Data.Reporting;
using TireForge.Data.Repositories;

namespace TireForge.Data.Tests;

/// <summary>Decision D13 — the <c>AgentCalls</c> metering table and the Cost read model.</summary>
public class AgentCostTests
{
    private static Reports NewReports(TestDb db) => new(
        new MachineStore(db.NewContext()),
        new DiagnosisStore(db.NewContext()),
        new WorkOrderStore(db.NewContext()),
        new ReportingQueries(db.NewContext()),
        new FixedClock(new DateTimeOffset(2026, 9, 1, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Recorder_writes_a_row_per_invocation()
    {
        using var db = new TestDb();
        var recorder = new AgentCallRecorder(db.NewContext());

        await recorder.RecordAsync(new AgentCallUsage(
            "anomaly-detection-agent", "gpt-5.4", 400, 120, 2, "rdg-1", "trace-abc"));

        var row = Assert.Single(db.NewContext().AgentCalls);
        Assert.Equal("anomaly-detection-agent", row.AgentName);
        Assert.Equal(400, row.PromptTokens);
        Assert.Equal(120, row.CompletionTokens);
        Assert.Equal(520, row.TotalTokens);
        Assert.Equal(2, row.ToolCalls);
        Assert.Equal("rdg-1", row.ReadingId);
        Assert.Equal("trace-abc", row.TraceId);
    }

    [Fact]
    public async Task Cost_with_no_metered_calls_reports_pending()
    {
        using var db = new TestDb();
        var cost = await NewReports(db).CostAsync();

        Assert.False(cost.TokenMetricsAvailable);
        Assert.All(cost.Agents, a => Assert.Null(a.Tokens));
        Assert.All(cost.Agents, a => Assert.Null(a.Spend));
    }

    [Fact]
    public async Task Cost_aggregates_tokens_and_prices_spend_per_agent()
    {
        using var db = new TestDb();
        var recorder = new AgentCallRecorder(db.NewContext());

        // two anomaly calls + one diagnosis call
        await recorder.RecordAsync(new AgentCallUsage("anomaly-detection-agent", "gpt-5.4", 1_000_000, 200_000, 1, "r1", "t1"));
        await recorder.RecordAsync(new AgentCallUsage("anomaly-detection-agent", "gpt-5.4", 0, 0, 0, "r2", "t2"));
        await recorder.RecordAsync(new AgentCallUsage("fault-diagnosis-agent", "gpt-5.4", 500_000, 100_000, 0, "r1", "t1"));

        var cost = await NewReports(db).CostAsync();

        Assert.True(cost.TokenMetricsAvailable);

        var anomaly = cost.Agents.Single(a => a.Agent == "Anomaly Detection");
        Assert.Equal(2, anomaly.Calls);
        Assert.Equal(1_200_000, anomaly.Tokens);
        // 1.0M in * $2.50 + 0.2M out * $10.00 = 2.50 + 2.00 = 4.50
        Assert.Equal(4.50m, anomaly.Spend);

        var diagnosis = cost.Agents.Single(a => a.Agent == "Fault Diagnosis");
        Assert.Equal(1, diagnosis.Calls);
        Assert.Equal(600_000, diagnosis.Tokens);
        // 0.5M * $2.50 + 0.1M * $10.00 = 1.25 + 1.00 = 2.25
        Assert.Equal(2.25m, diagnosis.Spend);
    }
}
