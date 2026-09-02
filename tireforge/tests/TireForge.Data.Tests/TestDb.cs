using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TireForge.Data;

namespace TireForge.Data.Tests;

/// <summary>
/// A throwaway SQLite database held in memory for the lifetime of the connection
/// (Decision D4 — same provider as prod, no files). Dispose to drop it.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _conn;

    public TireForgeDbContext Context { get; }

    public TestDb()
    {
        _conn = new SqliteConnection("Data Source=:memory:");
        _conn.Open();

        var options = new DbContextOptionsBuilder<TireForgeDbContext>()
            .UseSqlite(_conn)
            .Options;

        Context = new TireForgeDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>A fresh context over the same database (to read back without the tracking cache).</summary>
    public TireForgeDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<TireForgeDbContext>()
            .UseSqlite(_conn)
            .Options;
        return new TireForgeDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _conn.Dispose();
    }
}
