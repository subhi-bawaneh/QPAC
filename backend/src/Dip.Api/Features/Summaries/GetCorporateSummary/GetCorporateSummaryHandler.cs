using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Api.Features.Reports;
using Dip.Infrastructure.Persistence;

namespace Dip.Api.Features.Summaries.GetCorporateSummary;

public sealed class GetCorporateSummaryHandler
    : IQueryHandler<GetCorporateSummaryQuery, CorporateSummaryResponse>
{
    private readonly DipDbContext _db;

    public GetCorporateSummaryHandler(DipDbContext db) => _db = db;

    public async Task<CorporateSummaryResponse> Handle(
        GetCorporateSummaryQuery query, CancellationToken ct)
    {
        var data = await ReportDataLoader.LoadAsync(_db, query.ProjectId, ct);

        var summary = CorporateSummaryEngine.Compute(
            data.Documents, data.TrackerRows, data.Project, query.ReportDate);

        return new CorporateSummaryResponse(
            summary, data.RecalculationRequired, data.DocumentsWithoutSnapshot);
    }
}
