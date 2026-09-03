using Microsoft.EntityFrameworkCore;
using TireForge.Core.Model;

namespace TireForge.Data;

/// <summary>
/// EF Core context for the 5-table TireForge schema (Build Plan Stage A / §15):
/// Machines · Readings (+ IsAnomaly) · History · Diagnoses · WorkOrders.
/// Azure SQL (SqlServer provider); tests use a throwaway SQL Server container.
/// </summary>
public class TireForgeDbContext(DbContextOptions<TireForgeDbContext> options) : DbContext(options)
{
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<Reading> Readings => Set<Reading>();
    public DbSet<HistoryIncident> History => Set<HistoryIncident>();
    public DbSet<Diagnosis> Diagnoses => Set<Diagnosis>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<AgentCall> AgentCalls => Set<AgentCall>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Machine>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.Id).HasMaxLength(16);
            e.Property(m => m.Name).HasMaxLength(64);
            e.Property(m => m.Description).HasMaxLength(512);
            e.Property(m => m.SeedStatus).HasMaxLength(16);
            e.OwnsOne(m => m.Temperature);
            e.OwnsOne(m => m.Pressure);
            e.OwnsOne(m => m.Vibration);
            e.OwnsOne(m => m.Rpm);
        });

        b.Entity<Reading>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasMaxLength(48);
            e.Property(r => r.MachineId).HasMaxLength(16);
            e.HasOne(r => r.Machine).WithMany(m => m.Readings)
                .HasForeignKey(r => r.MachineId).OnDelete(DeleteBehavior.Cascade);
            e.Property(r => r.Mode).HasConversion<string?>().HasMaxLength(8);
            e.HasIndex(r => new { r.MachineId, r.CapturedAt });
        });

        b.Entity<HistoryIncident>(e =>
        {
            e.HasKey(h => h.Id);
            e.Property(h => h.Id).HasMaxLength(16);
            e.Property(h => h.MachineId).HasMaxLength(16);
            e.HasOne(h => h.Machine).WithMany()
                .HasForeignKey(h => h.MachineId).OnDelete(DeleteBehavior.Cascade);
            e.Property(h => h.Signature).HasMaxLength(64);
            e.Property(h => h.Fault).HasMaxLength(128);
            e.Property(h => h.Severity).HasConversion<string>().HasMaxLength(8);
            e.Property(h => h.Resolution).HasMaxLength(512);
            e.HasIndex(h => new { h.MachineId, h.Signature });
        });

        b.Entity<Diagnosis>(e =>
        {
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasMaxLength(48);
            e.Property(d => d.ReadingId).HasMaxLength(48);
            e.Property(d => d.MachineId).HasMaxLength(16);
            e.Property(d => d.Fault).HasMaxLength(128);
            e.Property(d => d.Severity).HasConversion<string>().HasMaxLength(8);
            e.Property(d => d.Route).HasConversion<string>().HasMaxLength(8);
            e.Property(d => d.Status).HasConversion<string>().HasMaxLength(16);
            e.Property(d => d.GateReason).HasMaxLength(128);
            e.Property(d => d.DraftActionText).HasMaxLength(1024);
            e.Property(d => d.IncidentCites).HasMaxLength(256);
            e.Property(d => d.TraceId).HasMaxLength(64);
            e.HasOne(d => d.Reading).WithMany()
                .HasForeignKey(d => d.ReadingId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(d => d.Status);
        });

        b.Entity<WorkOrder>(e =>
        {
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasMaxLength(48);
            e.Property(w => w.DiagnosisId).HasMaxLength(48);
            e.Property(w => w.MachineId).HasMaxLength(16);
            e.Property(w => w.ReadingId).HasMaxLength(48);
            e.Property(w => w.Fault).HasMaxLength(128);
            e.Property(w => w.Severity).HasConversion<string>().HasMaxLength(8);
            e.Property(w => w.Status).HasConversion<string>().HasMaxLength(8);
            e.Property(w => w.IssuedBy).HasMaxLength(64);
            e.Property(w => w.ActionText).HasMaxLength(1024);
            e.Property(w => w.RejectNote).HasMaxLength(512);
            e.HasOne(w => w.Diagnosis).WithOne(d => d.WorkOrder)
                .HasForeignKey<WorkOrder>(w => w.DiagnosisId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(w => w.Status);
        });

        b.Entity<AgentCall>(e =>
        {
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasMaxLength(48);
            e.Property(a => a.AgentName).HasMaxLength(48);
            e.Property(a => a.Model).HasMaxLength(32);
            e.Property(a => a.TraceId).HasMaxLength(64);
            e.Property(a => a.ReadingId).HasMaxLength(48);
            e.Ignore(a => a.TotalTokens);
            e.HasIndex(a => a.AgentName);
        });
    }
}
