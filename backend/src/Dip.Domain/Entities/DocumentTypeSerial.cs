using Dip.Domain.Common;

namespace Dip.Domain.Entities;

// How many digits the sequence field (F08C) carries for one document type.
// Editable from /lists like any other list, and soft-deleted the same way so an
// operator's decision survives a re-import (refactor-plan § 3 R9).
//
// A type with no row here uses SerialWidths.DefaultWidth.
public class DocumentTypeSerial : Entity
{
    public Guid ProjectId { get; set; }
    public string DocType { get; set; } = string.Empty;
    public int SequenceWidth { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public Project? Project { get; set; }
}
