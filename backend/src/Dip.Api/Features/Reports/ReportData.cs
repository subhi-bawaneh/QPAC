using Dip.Api.Common;
using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Reports;

// Everything the report engines need for one project, loaded once per request.
//
// Documents are the effective set (refactor-plan § 3 R8), so a company still working
// in Drive (Draft layer) counts in exactly the same reports as one working in the
// system (Live layer). The tracker rows come from the materialised DocumentSnapshots
// rather than from a fresh pass over the Aconex history: recalculation writes those,
// and a report that recomputed 25k revisions on every page view would not survive
// shared hosting. A row with no snapshot yet still appears, with empty computed values.
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

        var effective = await EffectiveDocumentLoader.LoadAsync(db, projectId, ct);

        var snapshots = await db.DocumentSnapshots
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .ToListAsync(ct);
        var snapshotsByRow = snapshots.ToDictionary(s => s.DocumentId);

        var documents = new List<Document>(effective.Count);
        var rows = new List<TrackerRow>(effective.Count);
        var missing = 0;

        foreach (var row in effective)
        {
            documents.Add(row.Document);
            if (snapshotsByRow.TryGetValue(row.RowId, out var snapshot))
            {
                rows.Add(TrackerRow.FromSnapshot(snapshot, row.Document.DocumentNumber));
            }
            else
            {
                missing++;
                rows.Add(new TrackerRow(
                    row.RowId, row.Document.DocumentNumber,
                    null, null, null, null, null, null, null, null, null, null, null));
            }
        }

        var baseline = await db.BaselineActivities
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .ToListAsync(ct);

        var statusMappings = await db.StatusMappings
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .ToListAsync(ct);

        return new ReportData(project, documents, rows, baseline, statusMappings, missing);
    }
}
