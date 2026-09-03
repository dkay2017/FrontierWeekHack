// TireForge.DbDeploy — the `azd postprovision` step for TireForge.Data.
//
//   dotnet run --project tireforge/tools/TireForge.DbDeploy -- \
//     --db "<Azure SQL connection string>" --apps "app1,app2,app3"
//
// 1. applies EF migrations to the (just-provisioned) Azure SQL database
// 2. grants each Function App's managed identity db_datareader + db_datawriter
//    (data-plane — bicep can't do this)
// 3. seeds the reference data (idempotent)
//
// Runs as the deploying user, who is the SQL Entra admin (Authentication=Active
// Directory Default picks up the `az login` token).

using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TireForge.Data;
using TireForge.Data.Seed;

var args0 = args.ToList();
string Arg(string n) => args0.IndexOf(n) is var i && i >= 0 && i + 1 < args0.Count
    ? args0[i + 1]
    : throw new ArgumentException($"missing {n}");

var connectionString = Arg("--db");
var appIdentities = Arg("--apps").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

Console.WriteLine($"server : {new SqlConnectionStringBuilder(connectionString).DataSource}");
Console.WriteLine($"apps   : {string.Join(", ", appIdentities)}\n");

// --- 1. migrations --------------------------------------------------------
await using (var db = new TireForgeDbContext(
    new DbContextOptionsBuilder<TireForgeDbContext>().UseSqlServer(connectionString).Options))
{
    Console.WriteLine("Applying migrations ...");
    await RetryAsync(() => db.Database.MigrateAsync());
    var applied = (await db.Database.GetAppliedMigrationsAsync()).ToList();
    Console.WriteLine($"  applied: {string.Join(", ", applied)}");

    // --- 3. seed (idempotent) ------------------------------------------
    Console.WriteLine("\nSeeding reference data ...");
    await DbSeeder.SeedAsync(db);
    Console.WriteLine($"  machines: {await db.Machines.CountAsync()}, history: {await db.History.CountAsync()}");
}

// --- 2. grant the Function App identities -------------------------------
Console.WriteLine("\nGranting Function App identities ...");
await using (var sql = new SqlConnection(connectionString))
{
    await RetryAsync(sql.OpenAsync);
    foreach (var identity in appIdentities)
    {
        var cmd = sql.CreateCommand();
        cmd.CommandText = """
            DECLARE @sql nvarchar(max);
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @name)
            BEGIN
                SET @sql = N'CREATE USER ' + QUOTENAME(@name) + N' FROM EXTERNAL PROVIDER;';
                EXEC sp_executesql @sql;
            END
            SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@name) + N';';
            EXEC sp_executesql @sql;
            SET @sql = N'ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@name) + N';';
            EXEC sp_executesql @sql;
            """;
        cmd.Parameters.Add("@name", System.Data.SqlDbType.NVarChar, 128).Value = identity;
        await RetryAsync(() => cmd.ExecuteNonQueryAsync());
        Console.WriteLine($"  {identity}: db_datareader + db_datawriter");
    }
}

Console.WriteLine("\nDone.");

static async Task RetryAsync(Func<Task> action, int attempts = 6)
{
    for (var i = 1; ; i++)
    {
        try { await action(); return; }
        catch (Exception ex) when (i < attempts)
        {
            Console.WriteLine($"  attempt {i} failed ({ex.Message.Split('\n')[0]}); retrying in {i * 10}s ...");
            await Task.Delay(TimeSpan.FromSeconds(i * 10));
        }
    }
}
