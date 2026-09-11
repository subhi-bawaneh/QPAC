using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class BaselineActivityConfiguration : IEntityTypeConfiguration<BaselineActivity>
{
    public void Configure(EntityTypeBuilder<BaselineActivity> b)
    {
        b.ToTable("BaselineActivities");
        b.HasKey(x => x.Id);
        b.Property(x => x.ActivityCode).HasMaxLength(50).IsRequired();
        b.Property(x => x.Package).HasMaxLength(500).IsRequired();
        b.Property(x => x.WbsLevel1).HasMaxLength(150);
        b.Property(x => x.WbsLevel2).HasMaxLength(150);
        b.Property(x => x.WbsLevel3).HasMaxLength(150);
        b.Property(x => x.WbsLevel4).HasMaxLength(150);
        b.Property(x => x.WbsLevel5).HasMaxLength(150);
        b.Property(x => x.WbsLevel6).HasMaxLength(150);
        b.Property(x => x.WbsLevel7).HasMaxLength(150);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Start).HasColumnType("timestamp without time zone");
        b.Property(x => x.Finish).HasColumnType("timestamp without time zone");
        b.Property(x => x.EditedBy).HasMaxLength(200);
        b.Property(x => x.EditedAt).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.ProjectId, x.ActivityCode }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.Package, x.Type });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
