using Dip.Api.Common;
using Dip.Api.Hubs;
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

// Rebuilds DocumentSnapshots — the materialised tracker rows every report and the
// Tracker grid read — a page at a time over the effective document set
// (refactor-plan § 3 R8), so both layers are covered by one pass.
//
// Still chunked: the worker loops the steps, and the step endpoint stays as the
// admin fallback. Each step loads only the Aconex revisions belonging to its own
// page, so the cost per step stays flat as the history grows.
public sealed class RecalculationService
{
    public const int DefaultChunkSize = 1000;
    public const int MaxChunkSize = 5000;

    // Snapshots are keyed by row id, so two overlapping rebuilds of the same project
    // race on the same primary keys. The worker runs one item at a time, but the
    // admin step endpoint can be called while it is working — this serialises them.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> Gates = new();

    private static SemaphoreSlim GateFor(Guid projectId) =>
        Gates.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1));

    private readonly DipDbContext _db;
    private readonly ISyncNotifier _notifier;

    public RecalculationService(DipDbContext db, ISyncNotifier notifier)
    {
        _db = db;
        _notifier = notifier;
    }

    public async Task<RecalculationStepResult> RunStepAsync(
        Guid projectId, int offset, int take, CancellationToken ct)
    {
        var gate = GateFor(projectId);
        await gate.WaitAsync(ct);
        try
        {
            return await RunStepCoreAsync(projectId, offset, take, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<RecalculationStepResult> RunStepCoreAsync(
        Guid projectId, int offset, int take, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        var effective = await EffectiveDocumentLoader.LoadAsync(_db, projectId, ct);
        var total = effective.Count;
        var chunk = Math.Clamp(take, 1, MaxChunkSize);

        // Ordered by row id so paging is stable across steps.
        var page = effective.OrderBy(d => d.RowId).Skip(offset).Take(chunk).ToList();
        if (page.Count == 0)
        {
            return new RecalculationStepResult(0, total, offset, Done: true);
        }

        await WritePageAsync(projectId, project, page, ct);

        var nextOffset = offset + page.Count;
        return new RecalculationStepResult(page.Count, total, nextOffset, nextOffset >= total);
    }

    // Loops the chunked step until the whole project is rebuilt, then drops the
    // snapshots of rows that are no longer in the effective set.
    public async Task<int> RunAllAsync(Guid projectId, CancellationToken ct)
    {
        var gate = GateFor(projectId);
        await gate.WaitAsync(ct);
        try
        {
            return await RunAllCoreAsync(projectId, ct);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<int> RunAllCoreAsync(Guid projectId, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw new KeyNotFoundException($"Project {projectId} not found");

        var effective = await EffectiveDocumentLoader.LoadAsync(_db, projectId, ct);
        var ordered = effective.OrderBy(d => d.RowId).ToList();

        for (var offset = 0; offset < ordered.Count; offset += DefaultChunkSize)
        {
            var page = ordered.Skip(offset).Take(DefaultChunkSize).ToList();
            await WritePageAsync(projectId, project, page, ct);
        }

        var keep = ordered.Select(d => d.RowId).ToHashSet();
        var stale = await _db.DocumentSnapshots
            .Where(s => s.ProjectId == projectId)
            .Select(s => s.DocumentId)
            .ToListAsync(ct);
        var drop = stale.Where(id => !keep.Contains(id)).ToList();
        if (drop.Count > 0)
        {
            await _db.DocumentSnapshots
                .Where(s => s.ProjectId == projectId && drop.Contains(s.DocumentId))
                .ExecuteDeleteAsync(ct);
        }

        await _notifier.RecalculationFinishedAsync(projectId, ordered.Count);
        return ordered.Count;
    }

    private async Task WritePageAsync(
        Guid projectId, Project project, IReadOnlyList<EffectiveDocument> page, CancellationToken ct)
    {
        var documents = page.Select(d => d.Document).ToList();
        var numbers = documents
            .Select(d => d.DocumentNumber.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var revisions = await _db.AconexRevisions
            .AsNoTracking()
            .Where(a => a.ProjectId == projectId
                && a.DocNoFinal != null
                && numbers.Contains(a.DocNoFinal.ToUpper()))
            .ToListAsync(ct);

        var baseline = await _db.BaselineActivities
            .AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .ToListAsync(ct);

        var statusMappings = await _db.StatusMappings
            .AsNoTracking()
            .Where(m => m.ProjectId == projectId && !m.IsDeleted)
            .ToListAsync(ct);

        var rows = TrackerEngine.Compute(documents, revisions, baseline, statusMappings, project);

        var rowIds = page.Select(d => d.RowId).ToList();
        var existing = await _db.DocumentSnapshots
            .Where(s => rowIds.Contains(s.DocumentId))
            .ToListAsync(ct);
        var existingByRow = existing.ToDictionary(s => s.DocumentId);
        var sourceByRow = page.ToDictionary(d => d.RowId);

        var computedAt = DateTime.UtcNow;
        foreach (var row in rows)
        {
            var computed = row.ToSnapshot(projectId, computedAt);
            Describe(computed, sourceByRow[row.DocumentId]);
            if (existingByRow.TryGetValue(row.DocumentId, out var snapshot))
            {
                Apply(snapshot, computed);
            }
            else
            {
                _db.DocumentSnapshots.Add(computed);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    // Copies the tracker's display columns off the source row so the grid can page
    // over DocumentSnapshots alone (decision D11).
    private static void Describe(DocumentSnapshot snapshot, EffectiveDocument source)
    {
        var document = source.Document;
        snapshot.Layer = source.Layer;
        snapshot.FolderFileId = source.FolderFileId;
        snapshot.DocumentNumber = document.DocumentNumber;
        snapshot.Title = document.Title;
        snapshot.Type = document.F04DocType;
        snapshot.Discipline = document.CorporateDiscipline;
        snapshot.Building = document.F07Building;
        snapshot.Level = document.F08BLevel;
        snapshot.Trade = document.F05Discipline;
        snapshot.Author = document.Exchanges.FirstOrDefault(e => e.Number == 1)?.Author;
        snapshot.DeliveryMilestone = document.DeliveryMilestone;
        snapshot.ActivityId = document.ActivityId;
        snapshot.PackageName = document.PackageName;
    }

    private static void Apply(DocumentSnapshot target, DocumentSnapshot computed)
    {
        target.ComputedAt = computed.ComputedAt;
        target.Layer = computed.Layer;
        target.FolderFileId = computed.FolderFileId;
        target.DocumentNumber = computed.DocumentNumber;
        target.Title = computed.Title;
        target.Type = computed.Type;
        target.Discipline = computed.Discipline;
        target.Building = computed.Building;
        target.Level = computed.Level;
        target.Trade = computed.Trade;
        target.Author = computed.Author;
        target.DeliveryMilestone = computed.DeliveryMilestone;
        target.ActivityId = computed.ActivityId;
        target.PackageName = computed.PackageName;
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
