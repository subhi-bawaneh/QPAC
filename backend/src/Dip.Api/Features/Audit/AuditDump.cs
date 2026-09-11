using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Audit;

// Replacing or deleting a source file destroys its rows, hand edits included. That is
// the owner's decision, and two things soften it without changing it: the dialog shows
// how many edited rows will go, and every outgoing row is written here first.
//
// One audit row per document, carrying the whole row as JSON — not one per field.
// A TIDP replace of 1,300 documents is then 1,300 rows rather than 30,000, and recovery
// is a query rather than a hope.
internal static class AuditDump
{
    // camelCase so the payload reads the same as every other JSON the API returns —
    // the history panel renders it straight through in stage 4.
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public const string Replaced = "Replaced";
    public const string Deleted = "Deleted";

    public static async Task<int> DocumentsAsync(
        DipDbContext db, Guid projectId, Guid tidpFileId, string action, string by, CancellationToken ct)
    {
        var documents = await db.Documents
            .AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => d.TidpFileId == tidpFileId)
            .ToListAsync(ct);

        var at = DateTime.UtcNow;
        foreach (var document in documents)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ProjectId = projectId,
                EntityName = nameof(Document),
                EntityId = document.Id,
                Action = action,
                Field = null,
                OldValue = JsonSerializer.Serialize(Snapshot(document), Json),
                NewValue = null,
                UserId = by,
                At = at,
            });
        }

        return documents.Count;
    }

    public static async Task<int> BaselineAsync(
        DipDbContext db, Guid projectId, string action, string by, CancellationToken ct)
    {
        var activities = await db.BaselineActivities
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .ToListAsync(ct);

        var at = DateTime.UtcNow;
        foreach (var activity in activities)
        {
            db.AuditLogs.Add(new AuditLog
            {
                ProjectId = projectId,
                EntityName = nameof(BaselineActivity),
                EntityId = activity.Id,
                Action = action,
                OldValue = JsonSerializer.Serialize(new
                {
                    activity.ActivityCode, activity.Package, activity.Type,
                    activity.OriginalDuration, activity.Start, activity.Finish,
                    activity.WbsLevel1, activity.WbsLevel2, activity.WbsLevel3, activity.WbsLevel4,
                    activity.WbsLevel5, activity.WbsLevel6, activity.WbsLevel7,
                    activity.IsEdited, activity.EditedBy, activity.EditedAt,
                }, Json),
                UserId = by,
                At = at,
            });
        }

        return activities.Count;
    }

    private static object Snapshot(Document d) => new
    {
        d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea, d.AuthoringSoftware,
        d.ExchangeFormat, d.Scale, d.DeliveryMilestone, d.PackageName, d.ActivityId,
        d.ClassificationCode,
        d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
        d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
        d.CorporateDiscipline, d.BudgetWeight,
        d.IsEdited, d.EditedBy, d.EditedAt,
        Exchanges = d.Exchanges.OrderBy(e => e.Number).Select(e => new
        {
            e.Number, e.Stage, e.ProgrammeRef, e.Author, e.Geometrical, e.NonGeometrical,
            e.DurationDays, e.Predecessor, e.ExchangeDate,
        }),
    };
}
