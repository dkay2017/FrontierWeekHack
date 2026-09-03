using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using TireForge.Data;

namespace TireForge.TestSupport;

/// <summary>
/// A throwaway in-memory SQLite database, held for the lifetime of the open
/// connection — the relational test double for <see cref="TireForgeDbContext"/>.
/// Production is Azure SQL / SqlServer (Decision D4); this keeps the test suite
/// fast, offline and hermetic. Dispose to drop it.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly SqliteConnection _connection;

    public TireForgeDbContext Context { get; }

    public TestDb()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();

        Context = NewContext();
        Context.Database.EnsureCreated();
    }

    /// <summary>A fresh context over the same database (no tracking-cache carry-over).</summary>
    public TireForgeDbContext NewContext()
        => new(new DbContextOptionsBuilder<TireForgeDbContext>()
            .UseSqlite(_connection)
            .Options);

    /// <summary>
    /// A DI provider action for <c>AddTireForgeData</c> that binds the context to
    /// this same in-memory database — for the host-wiring integration tests.
    /// </summary>
    public Action<DbContextOptionsBuilder> Configure => o => o.UseSqlite(_connection);

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
