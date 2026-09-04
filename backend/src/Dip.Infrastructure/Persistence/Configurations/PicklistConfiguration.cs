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
        b.HasIndex(x => new { x.ProjectId, x.Field, x.Code }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StatusMappingConfiguration : IEntityTypeConfiguration<StatusMapping>
{
    public void Configure(EntityTypeBuilder<StatusMapping> b)
    {
        b.ToTable("StatusMappings");
        b.HasKey(x => x.Id);
        b.Property(x => x.AconexStatus).HasMaxLength(100).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasIndex(x => new { x.ProjectId, x.AconexStatus }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
