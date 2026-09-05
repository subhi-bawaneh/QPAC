using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Reports;

// Everything the report engines need for one project, loaded once per request.
//
// The tracker rows come from the materialised DocumentSnapshots rather than from a
// fresh pass over the Aconex history: recalculation writes those (Phase 5.6), and a
// report that recomputed 25k revisions on every page view would not survive shared
// hosting. A document with no snapshot yet still appears, with empty computed values.
internal sealed record ReportData(
    Project Project,
    IReadOnlyList<Document> Documents,
    IReadOnlyList<TrackerRow> TrackerRows,
    IReadOnlyList<BaselineActivity> Baseline,
    IReadOnlyList<StatusMapping> StatusMappings,
    int DocumentsWithoutSnapshot)
{
    public bool RecalculationRequired => DocumentsWithoutSnapshot > 0;
}

internal static class ReportDataLoader
{
    public static async Task<ReportData> LoadAsync(
        DipDbContext db, Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        // Exchange 01's author drives the Corporate Summary's author breakdown, so the
        // exchanges come along; nothing else about them is read here.
        var documents = await db.Documents
            .AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => d.ProjectId == projectId)
            .ToListAsync(ct);

        var snapshots = await db.DocumentSnapshots
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync(ct);
        var snapshotsByDocument = snapshots.ToDictionary(s => s.DocumentId);

        var rows = new List<TrackerRow>(documents.Count);
        var missing = 0;
        foreach (var document in documents)
        {
            if (snapshotsByDocument.TryGetValue(document.Id, out var snapshot))
            {
                rows.Add(TrackerRow.FromSnapshot(snapshot, document.DocumentNumber));
            }
            else
            {
                missing++;
                rows.Add(new TrackerRow(
                    document.Id, document.DocumentNumber,
                    null, null, null, null, null, null, null, null, null, null, null));
            }
        }

        var baseline = await db.BaselineActivities
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .ToListAsync(ct);

        var statusMappings = await db.StatusMappings
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        return new ReportData(project, documents, rows, baseline, statusMappings, missing);
    }
}
