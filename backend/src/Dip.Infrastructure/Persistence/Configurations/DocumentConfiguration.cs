using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class TidpConfiguration : IEntityTypeConfiguration<Tidp>
{
    public void Configure(EntityTypeBuilder<Tidp> b)
    {
        b.ToTable("Tidps");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocumentReference).HasMaxLength(200).IsRequired();
        b.Property(x => x.RevisionNumber).HasMaxLength(10).IsRequired();
        b.Property(x => x.SourceFileName).HasMaxLength(300);
        b.Property(x => x.DateCreated).HasColumnType("timestamp without time zone");
        b.Property(x => x.DateLastUpdated).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => new { x.ProjectId, x.DisciplineId });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FolderFile).WithMany().HasForeignKey(x => x.FolderFileId).OnDelete(DeleteBehavior.SetNull);
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

        b.Property(x => x.DeliveryMilestone).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.UpdatedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasIndex(x => new { x.ProjectId, x.DocumentNumber }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.CorporateDiscipline });
        b.HasIndex(x => new { x.ProjectId, x.ActivityId });

        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Tidp).WithMany(x => x!.Documents).HasForeignKey(x => x.TidpId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FolderFile).WithMany().HasForeignKey(x => x.FolderFileId).OnDelete(DeleteBehavior.SetNull);
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
        b.Property(x => x.ExchangeDate).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.DocumentId, x.Number }).IsUnique();
        b.HasOne(x => x.Document)
            .WithMany(x => x!.Exchanges)
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
