using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class TidpDraftConfiguration : IEntityTypeConfiguration<TidpDraft>
{
    public void Configure(EntityTypeBuilder<TidpDraft> b)
    {
        b.ToTable("TidpDrafts");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentReference).HasMaxLength(200).IsRequired();
        b.Property(x => x.RevisionNumber).HasMaxLength(10).IsRequired();
        b.Property(x => x.SourceFileName).HasMaxLength(300);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DateCreated).HasColumnType("timestamp without time zone");
        b.Property(x => x.DateLastUpdated).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.ProjectId, x.FolderFileId });
        b.HasIndex(x => x.ImportBatchId);
    }
}

internal sealed class DocumentDraftConfiguration : IEntityTypeConfiguration<DocumentDraft>
{
    public void Configure(EntityTypeBuilder<DocumentDraft> b)
    {
        b.ToTable("DocumentDrafts");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentNumber).HasMaxLength(200).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500);
        b.Property(x => x.ExtractedFromModel).HasMaxLength(100);
        b.Property(x => x.ScopeArea).HasMaxLength(200);
        b.Property(x => x.AuthoringSoftware).HasMaxLength(100);
        b.Property(x => x.ExchangeFormat).HasMaxLength(100);
        b.Property(x => x.Scale).HasMaxLength(50);
        b.Property(x => x.PackageName).HasMaxLength(200);
        b.Property(x => x.ActivityId).HasMaxLength(50);
        b.Property(x => x.ClassificationCode).HasMaxLength(50);
        b.Property(x => x.F01Project).HasMaxLength(20);
        b.Property(x => x.F02Originator).HasMaxLength(20);
        b.Property(x => x.F03Contract).HasMaxLength(20);
        b.Property(x => x.F04DocType).HasMaxLength(20);
        b.Property(x => x.F05Discipline).HasMaxLength(20);
        b.Property(x => x.F06Zone).HasMaxLength(20);
        b.Property(x => x.F07Building).HasMaxLength(50);
        b.Property(x => x.F08ADrawingType).HasMaxLength(5);
        b.Property(x => x.F08BLevel).HasMaxLength(10);
        b.Property(x => x.F08CSequence).HasMaxLength(10);
        b.Property(x => x.CorporateDiscipline).HasMaxLength(100);
        b.Property(x => x.BudgetWeight).HasPrecision(10, 4);
        b.Property(x => x.ConflictReason).HasMaxLength(500);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DeliveryMilestone).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.ProjectId, x.FolderFileId, x.DocumentNumber });
        b.HasIndex(x => x.ImportBatchId);
        b.HasIndex(x => x.LiveDocumentId);
        b.HasOne<TidpDraft>().WithMany(x => x.Documents).HasForeignKey(x => x.TidpDraftId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class DataExchangeDraftConfiguration : IEntityTypeConfiguration<DataExchangeDraft>
{
    public void Configure(EntityTypeBuilder<DataExchangeDraft> b)
    {
        b.ToTable("DataExchangeDrafts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Stage).HasMaxLength(50);
        b.Property(x => x.ProgrammeRef).HasMaxLength(50);
        b.Property(x => x.Author).HasMaxLength(200);
        b.Property(x => x.Geometrical).HasMaxLength(50);
        b.Property(x => x.NonGeometrical).HasMaxLength(200);
        b.Property(x => x.Predecessor).HasMaxLength(200);
        b.Property(x => x.ExchangeDate).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.DocumentDraftId, x.Number }).IsUnique();
        b.HasOne<DocumentDraft>().WithMany(x => x.Exchanges).HasForeignKey(x => x.DocumentDraftId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PromoteBatchConfiguration : IEntityTypeConfiguration<PromoteBatch>
{
    public void Configure(EntityTypeBuilder<PromoteBatch> b)
    {
        b.ToTable("PromoteBatches");
        b.HasKey(x => x.Id);
        b.Property(x => x.By).HasMaxLength(200).IsRequired();
        b.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        b.Property(x => x.At).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.ProjectId, x.FolderFileId, x.At });
    }
}
