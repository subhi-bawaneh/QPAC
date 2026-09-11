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

        var revisions = await UnplannedRevisions.LoadAsync(_db, data, query.ProjectId, ct);

        var findings = ControlFindingsEngine.Compute(
            data.Documents, data.TrackerRows, revisions, data.Baseline, data.StatusMappings,
            data.Picklists);

        return new ControlFindingsResponse(
            findings, data.RecalculationRequired, data.DocumentsWithoutSnapshot);
    }
}
