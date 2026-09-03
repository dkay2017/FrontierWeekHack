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
        // Design-time only (dotnet ef). `migrations add` builds the model without
        // connecting; `database update` needs a real server — set TIREFORGE_DB.
        var cs = Environment.GetEnvironmentVariable("TIREFORGE_DB")
                 ?? "Server=localhost,1433;Database=tireforge;User Id=sa;Password=Your_password123;TrustServerCertificate=True;";
        var options = new DbContextOptionsBuilder<TireForgeDbContext>()
            .UseSqlServer(cs)
            .Options;
        return new TireForgeDbContext(options);
    }
}
