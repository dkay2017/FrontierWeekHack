using TireForge.Agents;
using TireForge.Core.Pipeline;
using TireForge.Data.Repositories;
using TireForge.Data.Seed;

namespace TireForge.Data.Tests;

/// <summary>Wires the real EF stores + the agent stubs into a <see cref="Pipeline"/> over a seeded in-memory DB.</summary>
internal sealed class PipelineHarness : IDisposable
{
    private readonly TestDb _db = new();

    public DateTimeOffset Now { get; } = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    private PipelineHarness() { }

    public static async Task<PipelineHarness> CreateAsync()
    {
        var h = new PipelineHarness();
        await DbSeeder.SeedAsync(h._db.Context);
        return h;
    }

    /// <summary>A pipeline whose stores all share one fresh context (mirrors a scoped DI lifetime).</summary>
    public Pipeline NewPipeline()
    {
        var ctx = _db.NewContext();
        return new Pipeline(
            new MachineStore(ctx),
            new ReadingStore(ctx),
            new HistoryStore(ctx),
            new DiagnosisStore(ctx),
            new WorkOrderStore(ctx),
            new StubAnomalyDetector(),
            new StubFaultDiagnoser(),
            new StubWorkOrderDrafter(),
            new FixedClock(Now));
    }

    public DiagnosisStore Diagnoses() => new(_db.NewContext());
    public WorkOrderStore WorkOrders() => new(_db.NewContext());
    public ReadingStore Readings() => new(_db.NewContext());

    public void Dispose() => _db.Dispose();
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}
