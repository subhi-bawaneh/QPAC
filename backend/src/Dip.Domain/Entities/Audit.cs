using Dip.Domain.Common;

namespace Dip.Domain.Entities;

public class AuditLog : Entity
{
    public Guid ProjectId { get; set; }
    public string EntityName { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Action { get; set; } = string.Empty;                 // "Create", "Update", "Delete"
    public string? Field { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime At { get; set; }
}
