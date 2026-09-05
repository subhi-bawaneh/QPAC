using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Api.Features.Reports;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.ControlFindings.GetControlFindings;

public sealed class GetControlFindingsHandler
    : IQueryHandler<GetControlFindingsQuery, ControlFindingsResponse>
{
    private readonly DipDbContext _db;

    public GetControlFindingsHandler(DipDbContext db) => _db = db;

    public async Task<ControlFindingsResponse> Handle(
        GetControlFindingsQuery query, CancellationToken ct)
    {
        var data = await ReportDataLoader.LoadAsync(_db, query.ProjectId, ct);

        // Report 1 is the only one that reads the Aconex history, and only its latest
        // rows that are absent from the MIDP — which the database can filter for us.
        var revisions = await _db.AconexRevisions
            .AsNoTracking()
            .Where(a => a.ProjectId == query.ProjectId && a.IsLatest && !a.InMidp)
            .ToListAsync(ct);

        var findings = ControlFindingsEngine.Compute(
            data.Documents, data.TrackerRows, revisions, data.Baseline, data.StatusMappings);

        return new ControlFindingsResponse(
            findings, data.RecalculationRequired, data.DocumentsWithoutSnapshot);
    }
}
