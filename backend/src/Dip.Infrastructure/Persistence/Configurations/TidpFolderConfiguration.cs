using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class TidpFolderOwnerConfiguration : IEntityTypeConfiguration<TidpFolderOwner>
{
    public void Configure(EntityTypeBuilder<TidpFolderOwner> b)
    {
        b.ToTable("TidpFolderOwners");
        b.HasKey(x => x.Id);
        b.Property(x => x.RelativePath).HasMaxLength(1000).IsRequired();
        b.Property(x => x.FolderName).HasMaxLength(400).IsRequired();
        b.Property(x => x.OwnerName).HasMaxLength(200);
        b.Property(x => x.OwnerType).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.FolderStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LastSeenAt).HasColumnType("datetime2");
        b.Property(x => x.MissingSince).HasColumnType("datetime2");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        // The upsert key. The default SQL Server collation is case-insensitive, which
        // is what the folder itself is on Windows, so `01.NAP` and `01.nap` collide
        // here exactly as they would on disk.
        b.HasIndex(x => new { x.ProjectId, x.RelativePath }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TidpFolderDisciplineConfiguration : IEntityTypeConfiguration<TidpFolderDiscipline>
{
    public void Configure(EntityTypeBuilder<TidpFolderDiscipline> b)
    {
        b.ToTable("TidpFolderDisciplines");
        b.HasKey(x => x.Id);
        b.Property(x => x.RelativePath).HasMaxLength(1000).IsRequired();
        b.Property(x => x.FolderName).HasMaxLength(400).IsRequired();
        b.Property(x => x.DisciplineCode).HasMaxLength(10).IsRequired();
        b.Property(x => x.DisciplineName).HasMaxLength(200).IsRequired();
        b.Property(x => x.FolderStatus).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.LastSeenAt).HasColumnType("datetime2");
        b.Property(x => x.MissingSince).HasColumnType("datetime2");
        b.Property(x => x.CreatedAt).HasColumnType("datetime2");
        b.Property(x => x.UpdatedAt).HasColumnType("datetime2");
        b.Property(x => x.CreatedBy).HasMaxLength(100);
        b.Property(x => x.UpdatedBy).HasMaxLength(100);
        b.Property(x => x.RowVersion).IsConcurrencyToken();

        b.HasIndex(x => new { x.ProjectId, x.RelativePath }).IsUnique();

        // Restrict on Project, Cascade on Owner: the owner already cascades from the
        // project, so cascading here too would be a second path to this table and SQL
        // Server refuses to create the foreign key at all (same shape as Documents).
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Owner).WithMany(x => x!.Disciplines).HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class TidpFolderSyncConfiguration : IEntityTypeConfiguration<TidpFolderSync>
{
    public void Configure(EntityTypeBuilder<TidpFolderSync> b)
    {
        b.ToTable("TidpFolderSyncs");
        b.HasKey(x => x.Id);
        b.Property(x => x.RootName).HasMaxLength(400);
        b.Property(x => x.StartedBy).HasMaxLength(200);
        b.Property(x => x.StartedAt).HasColumnType("datetime2");
        b.Property(x => x.ResultJson).HasColumnType("nvarchar(max)");
        b.HasIndex(x => new { x.ProjectId, x.StartedAt });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
