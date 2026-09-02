using TireForge.Core.Model;
using TireForge.Data.Repositories;
using TireForge.Data.Seed;

namespace TireForge.Data.Tests;

/// <summary>Build Plan Stage A check — step 4: data-access round-trips.</summary>
public class RepositoryTests
{
    [Fact]
    public async Task MachineStore_gets_a_seeded_machine()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);
        var store = new MachineStore(db.NewContext());

        var m = await store.GetAsync("EX-002");

        Assert.NotNull(m);
        Assert.Equal("extruder", m!.Name);
        Assert.Equal(15.0, m.Pressure.Max);
    }

    [Fact]
    public async Task MachineStore_returns_null_for_unknown_machine()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);
        var store = new MachineStore(db.NewContext());

        Assert.Null(await store.GetAsync("ZZ-999"));
    }

    [Fact]
    public async Task ReadingStore_round_trips_a_reading()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var reading = new Reading
        {
            Id = "rdg-test-001",
            MachineId = "MX-001",
            CapturedAt = DateTimeOffset.UtcNow,
            Temperature = 95, Pressure = 3.2, Vibration = 5.0, Rpm = 60,
            Mode = ReadingMode.Warn,
        };

        await new ReadingStore(db.NewContext()).AddAsync(reading);

        var back = await new ReadingStore(db.NewContext()).GetAsync("rdg-test-001");
        Assert.NotNull(back);
        Assert.Equal(95, back!.Temperature);
        Assert.Equal(ReadingMode.Warn, back.Mode);
        Assert.Null(back.IsAnomaly);
    }

    [Fact]
    public async Task ReadingStore_persists_is_anomaly_update()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var store = new ReadingStore(db.NewContext());
        var r = await store.GetAsync("rdg-seed-CP-003");
        r!.IsAnomaly = true;
        await store.UpdateAsync(r);

        var back = await new ReadingStore(db.NewContext()).GetAsync("rdg-seed-CP-003");
        Assert.True(back!.IsAnomaly);
    }

    [Fact]
    public async Task HistoryStore_matches_on_machine_and_signature()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);
        var store = new HistoryStore(db.NewContext());

        var hits = await store.MatchAsync("CP-003", "temp-high+vibration-high");

        Assert.Single(hits);
        Assert.Equal("inc-005", hits[0].Id);
        Assert.Equal(Severity.Crit, hits[0].Severity);
    }

    [Fact]
    public async Task WorkOrderStore_is_the_write_path_for_work_orders()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var reading = new Reading
        {
            Id = "rdg-test-002", MachineId = "CP-003", CapturedAt = DateTimeOffset.UtcNow,
            Temperature = 198, Pressure = 18, Vibration = 7, Rpm = 0,
        };
        await new ReadingStore(db.NewContext()).AddAsync(reading);

        var dx = new Diagnosis
        {
            Id = "dx-test-001", ReadingId = "rdg-test-002", MachineId = "CP-003",
            Fault = "platen bearing failure", Severity = Severity.Crit, Confidence = 0.83,
            Route = GateRoute.Review, Status = DiagnosisStatus.Pending, CreatedAt = DateTimeOffset.UtcNow,
        };
        await new DiagnosisStore(db.NewContext()).AddAsync(dx);

        var wo = new WorkOrder
        {
            Id = "WO-test-001", DiagnosisId = "dx-test-001", MachineId = "CP-003",
            Fault = "platen bearing failure", Severity = Severity.Crit, ReadingId = "rdg-test-002",
            ActionText = "Replace platen bearings", Status = WorkOrderStatus.Issued,
            IssuedBy = "system", CreatedAt = DateTimeOffset.UtcNow,
        };
        await new WorkOrderStore(db.NewContext()).AddAsync(wo);

        var list = await new WorkOrderStore(db.NewContext()).ListAsync();
        Assert.Single(list);
        Assert.Equal("WO-test-001", list[0].Id);
    }

    [Fact]
    public async Task DiagnosisStore_lists_pending_only()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        await new ReadingStore(db.NewContext()).AddAsync(new Reading
        {
            Id = "rdg-test-003", MachineId = "MX-001", CapturedAt = DateTimeOffset.UtcNow,
            Temperature = 95, Pressure = 3, Vibration = 5, Rpm = 60,
        });
        var store = new DiagnosisStore(db.NewContext());
        await store.AddAsync(new Diagnosis
        {
            Id = "dx-test-002", ReadingId = "rdg-test-003", MachineId = "MX-001",
            Fault = "blade imbalance", Severity = Severity.Warn, Confidence = 0.6,
            Route = GateRoute.Review, Status = DiagnosisStatus.Pending, CreatedAt = DateTimeOffset.UtcNow,
        });
        await store.AddAsync(new Diagnosis
        {
            Id = "dx-test-003", ReadingId = "rdg-test-003", MachineId = "MX-001",
            Fault = "overheating", Severity = Severity.Info, Confidence = 0.9,
            Route = GateRoute.Auto, Status = DiagnosisStatus.AutoIssued, CreatedAt = DateTimeOffset.UtcNow,
        });

        var pending = await new DiagnosisStore(db.NewContext()).PendingAsync();
        Assert.Single(pending);
        Assert.Equal("dx-test-002", pending[0].Id);
    }
}
