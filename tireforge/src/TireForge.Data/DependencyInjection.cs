using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TireForge.Core.Abstractions;
using TireForge.Core.Agents;
using TireForge.Core.Reporting;
using TireForge.Core.Reviewing;
using TireForge.Data.Reporting;
using TireForge.Data.Repositories;

namespace TireForge.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core context (Azure SQL / SqlServer), the write-path stores,
    /// the reporting queries, and the <see cref="Reports"/> / <see cref="Reviewer"/>
    /// services. Hosts add the agent implementations and <c>Pipeline</c> themselves.
    /// </summary>
    public static IServiceCollection AddTireForgeData(this IServiceCollection services, string connectionString)
        => services.AddTireForgeData(o => o.UseSqlServer(connectionString));

    /// <summary>
    /// As <see cref="AddTireForgeData(IServiceCollection, string)"/> but with an
    /// explicit provider configuration — tests inject an in-memory SQLite context
    /// this way (the relational test double; production is Azure SQL, Decision D4).
    /// </summary>
    public static IServiceCollection AddTireForgeData(
        this IServiceCollection services, Action<DbContextOptionsBuilder> configure)
    {
        services.AddDbContext<TireForgeDbContext>(configure);
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IMachineStore, MachineStore>();
        services.AddScoped<IReadingStore, ReadingStore>();
        services.AddScoped<IHistoryStore, HistoryStore>();
        services.AddScoped<IDiagnosisStore, DiagnosisStore>();
        services.AddScoped<IWorkOrderStore, WorkOrderStore>();
        services.AddScoped<IEarlyWarningStore, EarlyWarningStore>();
        services.AddScoped<IReportingQueries, ReportingQueries>();

        // Cost metering (D13) — overrides the no-op recorder from AddTireForgeAgents.
        services.RemoveAll<IAgentCallRecorder>();
        services.AddScoped<IAgentCallRecorder, AgentCallRecorder>();

        services.AddScoped<Reports>();
        services.AddScoped<Reviewer>();

        return services;
    }

    /// <summary>
    /// Create the schema and seed it (local / demo / tests). SqlServer applies
    /// migrations; SQLite (tests, no migration history) uses <c>EnsureCreated</c>.
    /// </summary>
    public static async Task InitializeTireForgeDataAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TireForgeDbContext>();

        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
            await db.Database.EnsureCreatedAsync(ct);
        else
            await db.Database.MigrateAsync(ct);

        await Seed.DbSeeder.SeedAsync(db, ct);
    }
}
