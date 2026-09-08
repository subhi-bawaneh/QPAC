using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tracker.GetTrackerDocument;

public sealed class GetTrackerDocumentHandler
    : IQueryHandler<GetTrackerDocumentQuery, TrackerDocumentDto>
{
    private readonly DipDbContext _db;

    public GetTrackerDocumentHandler(DipDbContext db) => _db = db;

    public async Task<TrackerDocumentDto> Handle(GetTrackerDocumentQuery query, CancellationToken ct)
    {
        var snapshot = await _db.DocumentSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.DocumentId == query.DocumentId, ct)
            ?? throw new KeyNotFoundException($"Document {query.DocumentId} not found");

        var statuses = StatusMappingLookup.Create(
            await _db.StatusMappings.AsNoTracking()
                .Where(m => m.ProjectId == snapshot.ProjectId && !m.IsDeleted)
                .ToListAsync(ct));

        var number = snapshot.DocumentNumber.ToUpperInvariant();
        var revisions = await _db.AconexRevisions
            .AsNoTracking()
            .Where(a => a.ProjectId == snapshot.ProjectId && a.DocNoFinal.ToUpper() == number)
            .OrderByDescending(a => a.DateModified)
            .ToListAsync(ct);

        // Terminated rows are shown — the drawer is the one place the history is meant
        // to be complete — but flagged, since the computed columns exclude them.
        var history = revisions
            .Select(a => new TrackerRevisionDto(
                a.Id, a.Revision,
                a.IsTerminated ? TrackerEngine.TerminatedStatus : a.AconexStatus,
                statuses.Find(a.IsTerminated ? TrackerEngine.TerminatedStatus : a.AconexStatus),
                a.ReviewStatus, a.DateModified, a.TransmittalIn,
                a.FileType, a.FileName, a.IsLatest, a.IsTerminated))
            .ToList();

        return new TrackerDocumentDto(TrackerRowDto.From(snapshot), history);
    }
}
