using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;

namespace Dip.Api.Common;

// Every mutating handler writes through here, so two things are true of every human
// edit rather than of the two handlers that happened to remember:
//
//   - one AuditLog row per changed field, with the old and new value. The refactor plan
//     deleted the rollback feature and named the audit log as its replacement; that is
//     only true if the log records everything a person changes.
//   - IsEdited / EditedBy / EditedAt set on the row, so the grid can mark hand-entered
//     data and the replace dialog can count what it is about to destroy.
internal static class Audited
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
    public const string Restore = "Restore";

    /// Writes one audit row per changed field and stamps the edit columns.
    public static void Changes(
        DipDbContext db,
        Guid projectId,
        string entityName,
        Guid entityId,
        IReadOnlyList<FieldChange> changes,
        string by,
        DateTime at)
    {
        foreach (var change in changes)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ProjectId = projectId,
                EntityName = entityName,
                EntityId = entityId,
                Action = Update,
                Field = change.Field,
                OldValue = change.OldValue,
                NewValue = change.NewValue,
                UserId = by,
                At = at,
            });
        }
    }

    /// A whole-row action — created, deleted, restored — where a field-level diff says
    /// nothing useful. `value` is whatever identifies the row to a person reading it.
    public static void Row(
        DipDbContext db,
        Guid projectId,
        string entityName,
        Guid entityId,
        string action,
        string? oldValue,
        string? newValue,
        string by,
        DateTime at)
    {
        db.AuditLogs.Add(new AuditLog
        {
            ProjectId = projectId,
            EntityName = entityName,
            EntityId = entityId,
            Action = action,
            Field = null,
            OldValue = oldValue,
            NewValue = newValue,
            UserId = by,
            At = at,
        });
    }

    public static void MarkEdited(PicklistItem item, string by, DateTime at)
    {
        item.IsEdited = true;
        item.EditedBy = by;
        item.EditedAt = at;
    }

    public static void MarkEdited(StatusMapping mapping, string by, DateTime at)
    {
        mapping.IsEdited = true;
        mapping.EditedBy = by;
        mapping.EditedAt = at;
    }

    public static void MarkEdited(BaselineActivity activity, string by, DateTime at)
    {
        activity.IsEdited = true;
        activity.EditedBy = by;
        activity.EditedAt = at;
    }

    public static void MarkEdited(Document document, string by, DateTime at)
    {
        document.IsEdited = true;
        document.EditedBy = by;
        document.EditedAt = at;
    }
}
