using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using TireForge.Agents;
using TireForge.Core.Model;
using TireForge.Core.Pipeline;
using TireForge.Data;
using TireForge.Orchestrator;

namespace TireForge.Orchestrator.Tests;

/// <summary>
/// The durable activity path — the same DI wiring `Program.cs` builds
/// (<c>AddTireForgeData</c> + <c>AddTireForgeAgents</c> + <c>Pipeline</c>), driven
/// end to end over a seeded in-memory DB with the agent stubs.
/// </summary>
public sealed class PipelineActivityTests : IAsyncLifetime, IDisposable
{
    private const string Conn = "Data Source=file:orch-tests?mode=memory&cache=shared";
    private readonly SqliteConnection _keepAlive = new(Conn);
    private ServiceProvider _sp = null!;

    public async Task InitializeAsync()
    {
        _keepAlive.Open();
        var services = new ServiceCollection();
        services.AddTireForgeData(Conn);
        services.AddTireForgeAgents();            // TIREFORGE_AGENTS unset -> stubs
        services.AddScoped<Pipeline>();
        services.AddScoped<PipelineFunctions>();
        _sp = services.BuildServiceProvider();
        await _sp.InitializeTireForgeDataAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
    public void Dispose() { _sp?.Dispose(); _keepAlive.Dispose(); }

    private static Reading Reading(string machineId, double t, double p, double v, double r) => new()
    {
        Id = Ids.Reading(DateTimeOffset.UtcNow),
        MachineId = machineId,
        CapturedAt = DateTimeOffset.UtcNow,
        Temperature = t, Pressure = p, Vibration = v, Rpm = r,
    };

    [Fact]
    public void Program_wiring_resolves_the_pipeline_and_its_functions()
    {
        using var scope = _sp.CreateScope();
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<Pipeline>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<PipelineFunctions>());
    }

    [Fact]
    public async Task Critical_reading_runs_the_pipeline_and_routes_to_review()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<PipelineFunctions>();

        var summary = await fn.RunPipeline(Reading("CP-003", 198.5, 18.2, 7.3, 0));

        Assert.True(summary.IsAnomaly);
        Assert.Equal("Crit", summary.ThresholdSeverity);
        Assert.Equal("Review", summary.GateRoute);
        Assert.NotNull(summary.DiagnosisId);
        Assert.Null(summary.WorkOrderId);                 // review route issues no WO
        Assert.Equal(32, summary.TraceId.Length);         // W3C trace id
        Assert.NotEmpty(summary.Trace);
    }

    [Fact]
    public async Task Normal_reading_stops_at_detection()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<PipelineFunctions>();

        var summary = await fn.RunPipeline(Reading("EX-002", 115, 12.5, 2.1, 30));

        Assert.False(summary.IsAnomaly);
        Assert.Null(summary.DiagnosisId);
        Assert.Null(summary.WorkOrderId);
    }

    [Fact]
    public async Task Confident_warning_auto_issues_a_work_order()
    {
        using var scope = _sp.CreateScope();
        var fn = scope.ServiceProvider.GetRequiredService<PipelineFunctions>();

        var summary = await fn.RunPipeline(Reading("IS-005", 24, 1.0, 5.2, 1800));

        Assert.True(summary.IsAnomaly);
        Assert.Equal("Auto", summary.GateRoute);
        Assert.NotNull(summary.WorkOrderId);
    }
}
