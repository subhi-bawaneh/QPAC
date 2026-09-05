using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Api.Features.Reports;
using Dip.Infrastructure.Persistence;

namespace Dip.Api.Features.Summaries.GetBaselineSummary;

public sealed class GetBaselineSummaryHandler
    : IQueryHandler<GetBaselineSummaryQuery, BaselineSummaryResponse>
{
    private readonly DipDbContext _db;

    public GetBaselineSummaryHandler(DipDbContext db) => _db = db;

    public async Task<BaselineSummaryResponse> Handle(
        GetBaselineSummaryQuery query, CancellationToken ct)
    {
        var data = await ReportDataLoader.LoadAsync(_db, query.ProjectId, ct);

        var summary = BaselineSummaryEngine.Compute(data.Documents, data.TrackerRows, data.Baseline);

        return new BaselineSummaryResponse(
            summary, data.RecalculationRequired, data.DocumentsWithoutSnapshot);
    }
}
