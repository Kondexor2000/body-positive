using BiasAudit.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BiasAudit.Api.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<AuditJob> AuditJobs => Set<AuditJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuditJob>(entity =>
        {
            entity.ToTable("audit_jobs");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.ModelDecision).HasMaxLength(64);
            entity.Property(x => x.Cohort).HasMaxLength(128);
            entity.Property(x => x.Notes).HasMaxLength(2000);
            entity.Property(x => x.NudeNetJson).HasColumnType("jsonb");
            entity.Property(x => x.FindingsJson).HasColumnType("jsonb");
            entity.Property(x => x.Error).HasMaxLength(2000);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.CreatedAt);
        });
    }
}
