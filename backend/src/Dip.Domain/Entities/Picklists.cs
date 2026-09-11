using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public class PicklistItem : Entity
{
    public Guid ProjectId { get; set; }
    public PicklistField Field { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    // Soft delete (refactor-plan § 3 R9): deleted codes stay out of every read and
    // are never resurrected by a re-import, so an operator's decision survives.
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsEdited { get; set; }
    public string? EditedBy { get; set; }
    public DateTime? EditedAt { get; set; }

    public Project? Project { get; set; }
}

public class StatusMapping : Entity
{
    public Guid ProjectId { get; set; }
    public string AconexStatus { get; set; } = string.Empty;
    public UnifiedStatus Status { get; set; }

    // Kept as data so operators can edit mappings from /admin/lists without a code change.
    public bool IsLegacy { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    public bool IsEdited { get; set; }
    public string? EditedBy { get; set; }
    public DateTime? EditedAt { get; set; }

    public Project? Project { get; set; }
}
