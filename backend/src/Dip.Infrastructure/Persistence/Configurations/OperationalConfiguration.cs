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
        // Keyed by Document.Id, with the foreign key back now that a snapshot can
        // only come from one table: deleting a document deletes its tracker row.
        b.HasKey(x => x.DocumentId);
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
        b.Property(x => x.ComputedAt).HasColumnType("datetime2");
        b.Property(x => x.DeliveryMilestone).HasColumnType("datetime2");
        b.Property(x => x.PlannedStart).HasColumnType("datetime2");
        b.Property(x => x.PlannedFinish).HasColumnType("datetime2");
        b.Property(x => x.ActualStart).HasColumnType("datetime2");
        b.Property(x => x.ActualFinish).HasColumnType("datetime2");
        b.Property(x => x.SubmissionDate).HasColumnType("datetime2");
        b.Property(x => x.DateModified).HasColumnType("datetime2");
        b.HasIndex(x => new { x.ProjectId, x.DocumentNumber });
        b.HasIndex(x => new { x.ProjectId, x.Discipline });
        b.HasIndex(x => new { x.ProjectId, x.Status });
        b.HasIndex(x => x.TidpFileId);
        b.HasOne(x => x.Document)
            .WithOne()
            .HasForeignKey<DocumentSnapshot>(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
        // ProjectId and TidpFileId are copied display columns, not navigations, but EF
        // still infers a foreign key for each from the property name. Left as Cascade
        // (the convention default) each would be a second cascade path to this table
        // alongside Document, which SQL Server refuses — Restrict here, the one real
        // path stays via Document.
        b.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<TidpFile>().WithMany().HasForeignKey(x => x.TidpFileId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> b)
    {
        b.ToTable("ImportBatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        b.Property(x => x.UploadedBy).HasMaxLength(200);
        b.Property(x => x.ImportedAt).HasColumnType("datetime2");
        b.Property(x => x.Log).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.ProjectId, x.ImportedAt });
        // Restrict, not the Cascade EF would infer by convention: TidpFile already
        // cascades from Project and SetNulls this row's TidpFileId, so a Cascade here
        // too would be a second path to the same table — SQL Server refuses that FK.
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TidpFile).WithMany().HasForeignKey(x => x.TidpFileId).OnDelete(DeleteBehavior.SetNull);
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
        // Unbounded: a Replaced or Deleted entry carries the whole outgoing row as
        // JSON, which is longer than any field-level diff.
        b.Property(x => x.OldValue).HasColumnType("nvarchar(max)");
        b.Property(x => x.NewValue).HasColumnType("nvarchar(max)");
        b.Property(x => x.UserId).HasMaxLength(100);
        b.Property(x => x.At).HasColumnType("datetime2");
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
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.ExpiresAt).HasColumnType("datetime2");
        b.Property(x => x.RevokedAt).HasColumnType("datetime2");
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
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
    }
}
