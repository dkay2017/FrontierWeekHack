using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Testcontainers.MsSql;
using TireForge.Data;

namespace TireForge.TestSupport;

/// <summary>
/// One throwaway SQL Server container per test process (Testcontainers). Each
/// <see cref="TestDb"/> / <c>NewConnectionString</c> gets its own fresh database
/// on that server, so tests stay isolated without a container-per-test cost.
///
/// Requires a Docker daemon — `dotnet test` does not run in a Codespace without
/// one (the agreed "develop in the Codespace, test + fix locally" split).
/// </summary>
public static class SqlServer
{
    private static readonly SemaphoreSlim _gate = new(1, 1);
    private static MsSqlContainer? _container;
    private static int _seq;

    private static async Task<MsSqlContainer> ContainerAsync()
    {
        if (_container is not null) return _container;
        await _gate.WaitAsync();
        try
        {
            if (_container is null)
            {
                var c = new MsSqlBuilder()
                    .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
                    .Build();
                await c.StartAsync();
                _container = c;
            }
        }
        finally { _gate.Release(); }
        return _container;
    }

    /// <summary>A connection string pointing at a fresh, not-yet-created database.</summary>
    public static async Task<string> NewConnectionStringAsync()
    {
        var container = await ContainerAsync();
        var name = $"tf_{Interlocked.Increment(ref _seq):D4}_{Guid.NewGuid():N}";
        return new SqlConnectionStringBuilder(container.GetConnectionString())
        {
            InitialCatalog = name,
        }.ConnectionString;
    }
}

/// <summary>
/// A fresh EF context over its own database on the shared container. Dispose to
/// drop it. Mirrors the old SQLite <c>TestDb</c> surface (<c>Context</c>,
/// <c>NewContext()</c>) so existing tests need no changes.
/// </summary>
public sealed class TestDb : IDisposable
{
    private readonly string _connectionString;

    public TireForgeDbContext Context { get; }

    private TestDb(string connectionString)
    {
        _connectionString = connectionString;
        Context = NewContext();
        Context.Database.EnsureCreated();
    }

    public static Task<TestDb> CreateAsync() => CreateInternalAsync();

    // Synchronous ctor shim: existing tests do `new TestDb()`. Block once on the
    // (usually already-warm) container; the DB create is fast.
    public TestDb() : this(SqlServer.NewConnectionStringAsync().GetAwaiter().GetResult()) { }

    private static async Task<TestDb> CreateInternalAsync()
        => new(await SqlServer.NewConnectionStringAsync());

    /// <summary>A fresh context over the same database (no tracking cache carry-over).</summary>
    public TireForgeDbContext NewContext()
        => new(new DbContextOptionsBuilder<TireForgeDbContext>()
            .UseSqlServer(_connectionString)
            .Options);

    public void Dispose()
    {
        try { Context.Database.EnsureDeleted(); } catch { /* best effort cleanup */ }
        Context.Dispose();
    }
}
