using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Recalculation;

public sealed record RecalculationStepResult(
    int Processed,
    int Total,
    int NextOffset,
    bool Done);

// Rebuilds DocumentSnapshots — the materialised TrackerEngine output every report
// reads — a page at a time.
//
// Chunked because shared hosting has no background workers (CLAUDE.md rule 9): the
// caller keeps posting steps until Done. State is the offset in the response, so
// nothing needs a batch table, and a step that fails can simply be retried.
//
// Each step loads only the Aconex revisions belonging to its own page of documents,
// so the cost per step stays flat no matter how large the history grows.
public sealed class RecalculationService
{
    public const int DefaultChunkSize = 1000;
    public const int MaxChunkSize = 5000;

    private readonly DipDbContext _db;

    public RecalculationService(DipDbContext db) => _db = db;

    public async Task<RecalculationStepResult> RunStepAsync(
        Guid projectId, int offset, int take, CancellationToken ct)
    {
        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        var total = await _db.Documents.CountAsync(d => d.ProjectId == projectId, ct);
        var chunk = Math.Clamp(take, 1, MaxChunkSize);

        // Ordered by id so paging is stable across steps.
        var documents = await _db.Documents
            .AsNoTracking()
            .Where(d => d.ProjectId == projectId)
            .OrderBy(d => d.Id)
            .Skip(offset)
            .Take(chunk)
            .ToListAsync(ct);

        if (documents.Count == 0)
        {
            return new RecalculationStepResult(0, total, offset, Done: true);
        }

        var numbers = documents
            .Select(d => d.DocumentNumber.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var revisions = await _db.AconexRevisions
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId && numbers.Contains(a.DocNoFinal.ToUpper()))
            .ToListAsync(ct);

        var baseline = await _db.BaselineActivities
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .ToListAsync(ct);

        var statusMappings = await _db.StatusMappings
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);

        var rows = TrackerEngine.Compute(documents, revisions, baseline, statusMappings, project);

        var documentIds = documents.Select(d => d.Id).ToList();
        var existing = await _db.DocumentSnapshots
            .Where(s => documentIds.Contains(s.DocumentId))
            .ToListAsync(ct);
        var existingByDocument = existing.ToDictionary(s => s.DocumentId);

        var computedAt = DateTime.UtcNow;
        foreach (var row in rows)
        {
            var computed = row.ToSnapshot(projectId, computedAt);
            if (existingByDocument.TryGetValue(row.DocumentId, out var snapshot))
            {
                Apply(snapshot, computed);
            }
            else
            {
                _db.DocumentSnapshots.Add(computed);
            }
        }

        await _db.SaveChangesAsync(ct);

        var nextOffset = offset + documents.Count;
        return new RecalculationStepResult(documents.Count, total, nextOffset, nextOffset >= total);
    }

    private static void Apply(DocumentSnapshot target, DocumentSnapshot computed)
    {
        target.ComputedAt = computed.ComputedAt;
        target.SubmissionsCount = computed.SubmissionsCount;
        target.Revision = computed.Revision;
        target.AconexStatus = computed.AconexStatus;
        target.Status = computed.Status;
        target.SubmissionDate = computed.SubmissionDate;
        target.DateModified = computed.DateModified;
        target.Transmittal = computed.Transmittal;
        target.PlannedStart = computed.PlannedStart;
        target.PlannedFinish = computed.PlannedFinish;
        target.ActualStart = computed.ActualStart;
        target.ActualFinish = computed.ActualFinish;
    }
}
