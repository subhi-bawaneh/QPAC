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
        // Restrict: an author picklist item that a folder points at must be
        // soft-deleted, not removed underneath the folder.
        b.HasOne(x => x.Author).WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict);
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
        b.Property(x => x.ContentMd5).HasMaxLength(64).IsRequired();
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ContentSource).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.State).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ImportError).HasMaxLength(2000);
        b.Property(x => x.DriveModifiedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.ContentModifiedAt).HasColumnType("timestamp without time zone");
        b.Property(x => x.LastImportedAt).HasColumnType("timestamp without time zone");
        b.HasIndex(x => x.DriveFileId);
        // R1: exactly one non-deleted file per (folder, case-insensitive name).
        // The comparison key is a stored generated column so the uniqueness is the
        // database's job, not the upsert code's.
        b.Property<string>("NameLower")
            .HasMaxLength(500)
            .HasComputedColumnSql("lower(\"Name\")", stored: true);
        b.HasIndex("FolderId", "NameLower")
            .IsUnique()
            .HasFilter("NOT \"IsDeleted\"")
            .HasDatabaseName("IX_FolderFiles_FolderId_NameLower_Active");
        b.HasOne(x => x.Folder)
            .WithMany(x => x!.Files)
            .HasForeignKey(x => x.FolderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class FileBlobConfiguration : IEntityTypeConfiguration<FileBlob>
{
    public void Configure(EntityTypeBuilder<FileBlob> b)
    {
        b.ToTable("FileBlobs");
        b.HasKey(x => x.FolderFileId);
        b.Property(x => x.Content).HasColumnType("bytea").IsRequired();
        b.HasOne(x => x.FolderFile)
            .WithOne(x => x!.Blob)
            .HasForeignKey<FileBlob>(x => x.FolderFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
