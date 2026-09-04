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

    public Project? Project { get; set; }
}

public class StatusMapping : Entity
{
    public Guid ProjectId { get; set; }
    public string AconexStatus { get; set; } = string.Empty;
    public UnifiedStatus Status { get; set; }

    // Kept as data so operators can edit mappings from /admin/lists without a code change.
    public bool IsLegacy { get; set; }

    public Project? Project { get; set; }
}
