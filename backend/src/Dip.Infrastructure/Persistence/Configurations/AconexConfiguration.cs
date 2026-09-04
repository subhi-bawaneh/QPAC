using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class AconexRevisionConfiguration : IEntityTypeConfiguration<AconexRevision>
{
    public void Configure(EntityTypeBuilder<AconexRevision> b)
    {
        b.ToTable("AconexRevisions");
        b.HasKey(x => x.Id);
        b.Property(x => x.FileType).HasMaxLength(20).IsRequired();
        b.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        b.Property(x => x.AconexDocNo).HasMaxLength(300).IsRequired();
        b.Property(x => x.DocNoFinal).HasMaxLength(200).IsRequired();
        b.Property(x => x.Revision).HasMaxLength(10).IsRequired();
        b.Property(x => x.Title).HasMaxLength(500);
        b.Property(x => x.AconexStatus).HasMaxLength(100).IsRequired();
        b.Property(x => x.ReviewStatus).HasMaxLength(100);
        b.Property(x => x.Type).HasMaxLength(100);
        b.Property(x => x.Discipline).HasMaxLength(100);
        b.Property(x => x.Area).HasMaxLength(200);
        b.Property(x => x.Venue).HasMaxLength(200);
        b.Property(x => x.FloorLevel).HasMaxLength(100);
        b.Property(x => x.TransmittalIn).HasMaxLength(200);
        // Postgres timestamp with microsecond precision (default 6). Sub-second precision preserved.
        b.Property(x => x.DateModified).HasColumnType("timestamp without time zone");
        b.HasIndex(x => new { x.ProjectId, x.AconexDocNo, x.Revision, x.DateModified }).IsUnique();
        b.HasIndex(x => new { x.ProjectId, x.DocNoFinal });
        b.HasIndex(x => new { x.ProjectId, x.DocNoFinal, x.IsLatest });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
