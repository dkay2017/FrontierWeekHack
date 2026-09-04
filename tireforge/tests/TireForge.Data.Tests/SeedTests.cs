using TireForge.Data.Seed;

namespace TireForge.Data.Tests;

/// <summary>Build Plan Stage A checks — steps 2 & 3.</summary>
public class SeedTests
{
    [Fact]
    public async Task Seed_creates_five_machines()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        Assert.Equal(5, db.NewContext().Machines.Count());
    }

    [Fact]
    public async Task Seed_machine_ids_match_the_factory_floor()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var ids = db.NewContext().Machines.Select(m => m.Id).OrderBy(x => x).ToArray();
        Assert.Equal(new[] { "CP-003", "CU-004", "EX-002", "IS-005", "MX-001" }, ids);
    }

    [Fact]
    public async Task Seed_populates_bands_and_units_from_sensor_data()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var mixer = db.NewContext().Machines.Single(m => m.Id == "MX-001");
        Assert.Equal(60, mixer.Temperature.Min);
        Assert.Equal(90, mixer.Temperature.Max);
        Assert.Equal("celsius", mixer.Temperature.Unit);
        Assert.Equal(4.5, mixer.Vibration.Max);
        Assert.Equal("mm/s", mixer.Vibration.Unit);
        Assert.Equal("warning", mixer.SeedStatus);
    }

    [Fact]
    public async Task Seed_writes_one_snapshot_reading_per_machine()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var readings = db.NewContext().Readings.ToList();
        Assert.Equal(5, readings.Count);
        Assert.All(readings, r => Assert.Null(r.IsAnomaly));

        var press = readings.Single(r => r.MachineId == "CP-003");
        Assert.Equal(198.5, press.Temperature);
    }

    [Fact]
    public async Task Seed_populates_history_with_at_least_eight_incidents()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);

        var history = db.NewContext().History.ToList();
        Assert.True(history.Count >= 8, $"expected >= 8 incidents, got {history.Count}");
        Assert.All(history, h => Assert.Contains(h.MachineId, new[] { "MX-001", "EX-002", "CP-003", "CU-004", "IS-005" }));
    }

    [Fact]
    public async Task Seed_is_idempotent()
    {
        using var db = new TestDb();
        await DbSeeder.SeedAsync(db.Context);
        await DbSeeder.SeedAsync(db.NewContext());

        Assert.Equal(5, db.NewContext().Machines.Count());
    }

    [Fact]
    public void Embedded_sensor_data_loads()
    {
        var file = SensorDataFile.Load();
        Assert.Equal(5, file.Machines.Count);
        Assert.Equal("Meridian Tire Manufacturing", file.Factory);
    }
}
