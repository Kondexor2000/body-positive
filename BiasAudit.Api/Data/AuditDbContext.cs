using BiasAudit.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BiasAudit.Api.Data;

public sealed class AuditDbContext(DbContextOptions<AuditDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<AuditJob> AuditJobs => Set<AuditJob>();
    public DbSet<RevokedToken> RevokedTokens => Set<RevokedToken>();
    public DbSet<PhotoPost> PhotoPosts => Set<PhotoPost>();
    public DbSet<PhotoTagConsent> PhotoTagConsents => Set<PhotoTagConsent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>()
            .HasIndex(x => x.Username)
            .IsUnique();

        modelBuilder.Entity<User>()
            .HasIndex(x => x.Email)
            .IsUnique();

        modelBuilder.Entity<RevokedToken>()
            .HasIndex(x => x.JwtId)
            .IsUnique();

        modelBuilder.Entity<PhotoPost>(entity =>
        {
            entity.ToTable("photo_posts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ObjectKey).HasMaxLength(512).IsRequired();
            entity.Property(x => x.OriginalFileName).HasMaxLength(256).IsRequired();
            entity.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PublicationStatus).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => x.PublicationStatus);
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<PhotoTagConsent>(entity =>
        {
            entity.ToTable("photo_tag_consents");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
            entity.HasIndex(x => new { x.PhotoPostId, x.TaggedUserId }).IsUnique();
            entity.HasIndex(x => new { x.TaggedUserId, x.Status });
            entity.HasOne(x => x.PhotoPost).WithMany(x => x.TagConsents)
                .HasForeignKey(x => x.PhotoPostId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(x => x.TaggedUser).WithMany().HasForeignKey(x => x.TaggedUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
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
