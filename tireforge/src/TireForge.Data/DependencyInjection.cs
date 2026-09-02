using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TireForge.Core.Abstractions;
using TireForge.Core.Reporting;
using TireForge.Core.Reviewing;
using TireForge.Data.Reporting;
using TireForge.Data.Repositories;

namespace TireForge.Data;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core context (SQLite), the write-path stores, the reporting
    /// queries, and the <see cref="Reports"/> / <see cref="Reviewer"/> services.
    /// Hosts add the agent implementations and <c>Pipeline</c> themselves.
    /// </summary>
    public static IServiceCollection AddTireForgeData(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<TireForgeDbContext>(o => o.UseSqlite(connectionString));
        services.AddSingleton(TimeProvider.System);

        services.AddScoped<IMachineStore, MachineStore>();
        services.AddScoped<IReadingStore, ReadingStore>();
        services.AddScoped<IHistoryStore, HistoryStore>();
        services.AddScoped<IDiagnosisStore, DiagnosisStore>();
        services.AddScoped<IWorkOrderStore, WorkOrderStore>();
        services.AddScoped<IReportingQueries, ReportingQueries>();

        services.AddScoped<Reports>();
        services.AddScoped<Reviewer>();

        return services;
    }

    /// <summary>Apply migrations and seed on startup (local / demo).</summary>
    public static async Task InitializeTireForgeDataAsync(this IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TireForgeDbContext>();
        await db.Database.MigrateAsync(ct);
        await Seed.DbSeeder.SeedAsync(db, ct);
    }
}
