using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace TireForge.Data;

/// <summary>
/// Design-time factory so <c>dotnet ef migrations</c> can build the model without
/// a running host. Runtime hosts configure the context via DI instead.
/// </summary>
public sealed class TireForgeDbContextFactory : IDesignTimeDbContextFactory<TireForgeDbContext>
{
    public TireForgeDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("TIREFORGE_DB")
                 ?? "Data Source=tireforge.db";
        var options = new DbContextOptionsBuilder<TireForgeDbContext>()
            .UseSqlite(cs)
            .Options;
        return new TireForgeDbContext(options);
    }
}
