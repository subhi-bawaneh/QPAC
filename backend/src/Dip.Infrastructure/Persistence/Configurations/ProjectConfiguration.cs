using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> b)
    {
        b.ToTable("Projects");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Client).HasMaxLength(200);
        b.Property(x => x.Organisation).HasMaxLength(200);
        b.Property(x => x.Approver).HasMaxLength(200);
        b.Property(x => x.CostCenter).HasMaxLength(50);
        b.Property(x => x.ReportDate).HasColumnType("timestamp without time zone");
        b.Property(x => x.BaselineStartDate).HasColumnType("timestamp without time zone");
        b.Property(x => x.WeightPending).HasPrecision(6, 4);
        b.Property(x => x.WeightSub1).HasPrecision(6, 4);
        b.Property(x => x.WeightSub2).HasPrecision(6, 4);
        b.Property(x => x.WeightApproved).HasPrecision(6, 4);
        b.HasIndex(x => x.Code).IsUnique();
    }
}

internal sealed class DisciplineConfiguration : IEntityTypeConfiguration<Discipline>
{
    public void Configure(EntityTypeBuilder<Discipline> b)
    {
        b.ToTable("Disciplines");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.CorporateName).HasMaxLength(100).IsRequired();
        b.HasIndex(x => new { x.ProjectId, x.Code }).IsUnique();
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
