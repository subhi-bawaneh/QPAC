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
        // Nullable: a raw value that is not a document number has no final number.
        b.Property(x => x.DocNoFinal).HasMaxLength(200);
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
        // datetime2 defaults to 100ns precision — finer than Aconex ever provides, so
        // sub-second Date Modified survives intact (hard rule 6).
        b.Property(x => x.DateModified).HasColumnType("datetime2");
        b.Property(x => x.LineHash).HasMaxLength(64).IsRequired();
        // Identity is the whole line: an upload inserts what this index does not
        // already hold. See AconexLineHasher for why it is not (doc, rev, date).
        b.HasIndex(x => new { x.ProjectId, x.LineHash }).IsUnique();
        // Not unique any more — Aconex can re-export one event with a corrected
        // title — but the flag pass and the tracker both group on it.
        b.HasIndex(x => new { x.ProjectId, x.AconexDocNo, x.Revision, x.DateModified });
        b.HasIndex(x => new { x.ProjectId, x.DocNoFinal });
        b.HasIndex(x => new { x.ProjectId, x.DocNoFinal, x.IsLatest });
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
