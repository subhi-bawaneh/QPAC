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
        b.HasKey(x => x.DocumentId);
        b.Property(x => x.Revision).HasMaxLength(10);
        b.Property(x => x.AconexStatus).HasMaxLength(100);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Transmittal).HasMaxLength(200);
        b.Property(x => x.ComputedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.PlannedStart).HasColumnType("timestamp without time zone");
        b.Property(x => x.PlannedFinish).HasColumnType("timestamp without time zone");
        b.Property(x => x.ActualStart).HasColumnType("timestamp without time zone");
        b.Property(x => x.ActualFinish).HasColumnType("timestamp without time zone");
        b.Property(x => x.SubmissionDate).HasColumnType("timestamp without time zone");
        b.Property(x => x.DateModified).HasColumnType("timestamp without time zone");
        b.HasOne(x => x.Document).WithOne().HasForeignKey<DocumentSnapshot>(x => x.DocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.ProjectId);
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

internal sealed class ImportStagingRowConfiguration : IEntityTypeConfiguration<ImportStagingRow>
{
    public void Configure(EntityTypeBuilder<ImportStagingRow> b)
    {
        b.ToTable("ImportStagingRows");
        b.HasKey(x => x.Id);
        b.Property(x => x.PayloadJson).HasColumnType("jsonb");
        b.Property(x => x.Error).HasMaxLength(1000);
        b.HasIndex(x => new { x.ImportBatchId, x.Processed });
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
