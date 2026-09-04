using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class FolderConfiguration : IEntityTypeConfiguration<Folder>
{
    public void Configure(EntityTypeBuilder<Folder> b)
    {
        b.ToTable("Folders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(300).IsRequired();
        b.Property(x => x.Path).HasMaxLength(1000).IsRequired();
        b.Property(x => x.DriveFolderId).HasMaxLength(100);
        b.Property(x => x.Target).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.LastSyncedAt).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.ProjectId, x.Path }).IsUnique();
        b.HasIndex(x => x.DriveFolderId);
        b.HasOne(x => x.Parent)
            .WithMany(x => x!.Children)
            .HasForeignKey(x => x.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FolderFileConfiguration : IEntityTypeConfiguration<FolderFile>
{
    public void Configure(EntityTypeBuilder<FolderFile> b)
    {
        b.ToTable("FolderFiles");
        b.HasKey(x => x.Id);
        b.Property(x => x.Name).HasMaxLength(500).IsRequired();
        b.Property(x => x.DriveFileId).HasMaxLength(100);
        b.Property(x => x.Md5).HasMaxLength(64);
        b.Property(x => x.StoragePath).HasMaxLength(500);
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DriveModifiedAt).HasColumnType("timestamp without time zone");
        b.HasIndex(x => x.DriveFileId);
        b.HasIndex(x => new { x.FolderId, x.Name });
        b.HasOne(x => x.Folder)
            .WithMany(x => x!.Files)
            .HasForeignKey(x => x.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
