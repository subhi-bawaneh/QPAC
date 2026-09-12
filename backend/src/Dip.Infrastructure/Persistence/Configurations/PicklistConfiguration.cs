using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class PicklistItemConfiguration : IEntityTypeConfiguration<PicklistItem>
{
    public void Configure(EntityTypeBuilder<PicklistItem> b)
    {
        b.ToTable("PicklistItems");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.Property(x => x.Field).HasConversion<string>().HasMaxLength(40);
        b.Property(x => x.DeletedAt).HasColumnType("datetime2");
        b.Property(x => x.EditedBy).HasMaxLength(200);
        b.Property(x => x.EditedAt).HasColumnType("datetime2");
        // Uniqueness applies to the live codes only, so a code can be soft-deleted
        // and later re-created (refactor-plan § 3 R9).
        b.HasIndex(x => new { x.ProjectId, x.Field, x.Code })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StatusMappingConfiguration : IEntityTypeConfiguration<StatusMapping>
{
    public void Configure(EntityTypeBuilder<StatusMapping> b)
    {
        b.ToTable("StatusMappings");
        b.HasKey(x => x.Id);
        // Case-sensitive: SeedData deliberately seeds both "No Longer In Use" (modern
        // Aconex) and "No Longer in Use" (legacy) as distinct rows, one flagged
        // IsLegacy. SQL Server's default collation is case-insensitive and would
        // collide the two on the unique index below; Postgres's default collation
        // is case-sensitive, which is the behavior this preserves.
        b.Property(x => x.AconexStatus).HasMaxLength(100).IsRequired().UseCollation("Latin1_General_100_CS_AS");
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.DeletedAt).HasColumnType("datetime2");
        b.Property(x => x.EditedBy).HasMaxLength(200);
        b.Property(x => x.EditedAt).HasColumnType("datetime2");
        b.HasIndex(x => new { x.ProjectId, x.AconexStatus })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0");
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
