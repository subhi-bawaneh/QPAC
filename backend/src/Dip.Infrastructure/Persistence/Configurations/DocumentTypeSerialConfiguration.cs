using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Dip.Infrastructure.Persistence.Configurations;

internal sealed class DocumentTypeSerialConfiguration : IEntityTypeConfiguration<DocumentTypeSerial>
{
    public void Configure(EntityTypeBuilder<DocumentTypeSerial> b)
    {
        b.ToTable("DocumentTypeSerials");
        b.HasKey(x => x.Id);
        b.Property(x => x.DocType).HasMaxLength(20).IsRequired();
        b.Property(x => x.DeletedAt).HasColumnType("timestamp without time zone");
        // Live codes only, so a type can be soft-deleted and re-created.
        b.HasIndex(x => new { x.ProjectId, x.DocType })
            .IsUnique()
            .HasFilter("NOT \"IsDeleted\"");
        b.HasOne(x => x.Project).WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Cascade);
    }
}
