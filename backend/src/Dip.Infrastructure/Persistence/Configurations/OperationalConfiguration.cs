using Dip.Domain.Entities;
using Dip.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class DocumentSnapshotConfiguration : IEntityTypeConfiguration<DocumentSnapshot>
{
    public void Configure(EntityTypeBuilder<DocumentSnapshot> b)
    {
        b.ToTable("DocumentSnapshots");
        // Keyed by the source row id (Document.Id or DocumentDraft.Id). No FK:
        // the row can live in either layer's table (decision D11).
        b.HasKey(x => x.DocumentId);
        b.Property(x => x.Layer).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.DocumentNumber).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.Type).HasMaxLength(20).IsRequired();
        b.Property(x => x.Discipline).HasMaxLength(100).IsRequired();
        b.Property(x => x.Building).HasMaxLength(50).IsRequired();
        b.Property(x => x.Level).HasMaxLength(10).IsRequired();
        b.Property(x => x.Trade).HasMaxLength(20).IsRequired();
        b.Property(x => x.Author).HasMaxLength(200);
        b.Property(x => x.ActivityId).HasMaxLength(50);
        b.Property(x => x.PackageName).HasMaxLength(200);
        b.Property(x => x.Revision).HasMaxLength(10);
        b.Property(x => x.AconexStatus).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Transmittal).HasMaxLength(200);
        b.Property(x => x.ComputedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.DeliveryMilestone).HasColumnType("timestamp without time zone");
        b.Property(x => x.PlannedStart).HasColumnType("timestamp without time zone");
        b.Property(x => x.PlannedFinish).HasColumnType("timestamp without time zone");
        b.Property(x => x.ActualStart).HasColumnType("timestamp without time zone");
        b.Property(x => x.ActualFinish).HasColumnType("timestamp without time zone");
        b.Property(x => x.SubmissionDate).HasColumnType("timestamp without time zone");
        b.Property(x => x.DateModified).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.ProjectId, x.Layer });
        b.HasIndex(x => new { x.ProjectId, x.DocumentNumber });
        b.HasIndex(x => new { x.ProjectId, x.Discipline });
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => x.FolderFileId);
    }
}

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.ToTable("ImportBatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Target).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        b.Property(x => x.ImportedBy).HasMaxLength(200);
        b.Property(x => x.ImportedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.Log).HasColumnType("jsonb");
        b.HasIndex(x => new { x.ProjectId, x.ImportedAt });
    }
}

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> b)
    {
        b.ToTable("AuditLogs");
        b.HasKey(x => x.Id);
        b.Property(x => x.EntityName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Action).HasMaxLength(50).IsRequired();
        b.Property(x => x.Field).HasMaxLength(100);
        b.Property(x => x.OldValue).HasMaxLength(2000);
        b.Property(x => x.NewValue).HasMaxLength(2000);
        b.Property(x => x.UserId).HasMaxLength(100);
        b.Property(x => x.At).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.ProjectId, x.EntityName, x.EntityId });
        b.HasIndex(x => x.At);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("RefreshTokens");
        b.HasKey(x => x.Id);
        b.Property(x => x.Token).HasMaxLength(256).IsRequired();
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedByIp).HasMaxLength(64);
        b.Property(x => x.ReplacedByToken).HasMaxLength(256);
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.ExpiresAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.RevokedAt).HasColumnType("timestamp without time zone");
        b.Ignore(x => x.IsActive);
        b.HasIndex(x => x.Token).IsUnique();
        b.HasOne(x => x.User).WithMany(x => x!.RefreshTokens).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(200);
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
    }
}
