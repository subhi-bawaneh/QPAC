using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class TidpFileConfiguration : IEntityTypeConfiguration<TidpFile>
{
    public void Configure(EntityTypeBuilder<TidpFile> b)
    {
        b.ToTable("TidpFiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentReference).HasMaxLength(200).IsRequired();
        b.Property(x => x.RevisionNumber).HasMaxLength(10).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        b.Property(x => x.UploadedBy).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Error).HasMaxLength(2000);
        b.Property(x => x.DateCreated).HasColumnType("datetime2");
        b.Property(x => x.DateLastUpdated).HasColumnType("datetime2");
        b.Property(x => x.UploadedAt).HasColumnType("datetime2");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        // Folder identity — null for the one-file-at-a-time upload route.
        b.Property(x => x.RelativePath).HasMaxLength(1000);
        b.Property(x => x.ContentHash).HasMaxLength(64);
        b.Property(x => x.FolderStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LastModifiedUtc).HasColumnType("datetime2");
        b.Property(x => x.LastSeenAt).HasColumnType("datetime2");
        b.Property(x => x.MissingSince).HasColumnType("datetime2");
        b.Property(x => x.NameProjectCode).HasMaxLength(20);
        b.Property(x => x.NameOriginator).HasMaxLength(20);
        b.Property(x => x.NameContract).HasMaxLength(20);
        b.Property(x => x.NameDocType).HasMaxLength(20);
        b.Property(x => x.DisciplineTag).HasMaxLength(10);
        b.Property(x => x.NameZone).HasMaxLength(20);
        b.Property(x => x.NameLevel).HasMaxLength(20);
        b.Property(x => x.Sequence).HasMaxLength(20);

        b.HasIndex(x => new { x.ProjectId, x.DisciplineId });
        b.HasIndex(x => new { x.ProjectId, x.UploadedAt });
        b.HasIndex(x => new { x.ProjectId, x.DisciplineTag });

        // Filtered, because a single-file upload leaves RelativePath null and SQL
        // Server treats two nulls as equal in a unique index — the second such upload
        // would be rejected as a duplicate of the first.
        b.HasIndex(x => new { x.ProjectId, x.RelativePath })
            .IsUnique()
            .HasFilter("[RelativePath] IS NOT NULL");

        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);

        // Restrict on both: TidpFile already cascades from Project, and so do the two
        // folder tables, so a cascade here would be a third path into this table.
        b.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FolderDiscipline).WithMany().HasForeignKey(x => x.FolderDisciplineId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> b)
    {
        b.ToTable("Documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentNumber).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500).IsRequired();
        b.Property(x => x.ExtractedFromModel).HasMaxLength(100);
        b.Property(x => x.ScopeArea).HasMaxLength(200);
        b.Property(x => x.AuthoringSoftware).HasMaxLength(100);
        b.Property(x => x.ExchangeFormat).HasMaxLength(100);
        b.Property(x => x.Scale).HasMaxLength(50);
        b.Property(x => x.PackageName).HasMaxLength(200);
        b.Property(x => x.ActivityId).HasMaxLength(50);
        b.Property(x => x.ClassificationCode).HasMaxLength(50);

        b.Property(x => x.F01Project).HasMaxLength(20).IsRequired();
        b.Property(x => x.F02Originator).HasMaxLength(20).IsRequired();
        b.Property(x => x.F03Contract).HasMaxLength(20).IsRequired();
        b.Property(x => x.F04DocType).HasMaxLength(20).IsRequired();
        b.Property(x => x.F05Discipline).HasMaxLength(20).IsRequired();
        b.Property(x => x.F06Zone).HasMaxLength(20).IsRequired();
        b.Property(x => x.F07Building).HasMaxLength(50).IsRequired();
        b.Property(x => x.F08ADrawingType).HasMaxLength(5).IsRequired();
        b.Property(x => x.F08BLevel).HasMaxLength(10).IsRequired();
        b.Property(x => x.F08CSequence).HasMaxLength(10).IsRequired();

        b.Property(x => x.CorporateDiscipline).HasMaxLength(100).IsRequired();
        b.Property(x => x.BudgetWeight).HasPrecision(10, 4);

        b.Property(x => x.EditedBy).HasMaxLength(200);
        b.Property(x => x.EditedAt).HasColumnType("datetime2");

        b.Property(x => x.DeliveryMilestone).HasColumnType("datetime2");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasIndex(x => new { x.ProjectId, x.DocumentNumber }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.CorporateDiscipline });
        b.HasIndex(x => new { x.ProjectId, x.ActivityId });
        b.HasIndex(x => new { x.ProjectId, x.TidpFileId });

        // Restrict, not Cascade: a Document's TidpFileId is required, and TidpFile
        // already cascades from Project, so this would otherwise be a second cascade
        // path to the same table — SQL Server refuses to create that FK at all.
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.TidpFile).WithMany(x => x!.Documents).HasForeignKey(x => x.TidpFileId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class DataExchangeConfiguration : IEntityTypeConfiguration<DataExchange>
{
    public void Configure(EntityTypeBuilder<DataExchange> b)
    {
        b.ToTable("DataExchanges");
        b.HasKey(x => x.Id);
        b.Property(x => x.Stage).HasMaxLength(50);
        b.Property(x => x.ProgrammeRef).HasMaxLength(50);
        b.Property(x => x.Author).HasMaxLength(200);
        b.Property(x => x.Geometrical).HasMaxLength(50);
        b.Property(x => x.NonGeometrical).HasMaxLength(200);
        b.Property(x => x.Predecessor).HasMaxLength(200);
        b.Property(x => x.ExchangeDate).HasColumnType("datetime2");
        b.HasIndex(x => new { x.DocumentId, x.Number }).IsUnique();
        b.HasOne(x => x.Document)
            .WithMany(x => x!.Exchanges)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
