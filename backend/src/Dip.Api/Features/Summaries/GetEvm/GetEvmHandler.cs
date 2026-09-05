using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Api.Features.Reports;
using Dip.Infrastructure.Persistence;

namespace Dip.Api.Features.Summaries.GetEvm;

public sealed class GetEvmHandler : IQueryHandler<GetEvmQuery, EvmResponse>
{
    private readonly DipDbContext _db;

    public GetEvmHandler(DipDbContext db) => _db = db;

    public async Task<EvmResponse> Handle(GetEvmQuery query, CancellationToken ct)
    {
        var data = await ReportDataLoader.LoadAsync(_db, query.ProjectId, ct);

        var summary = EvmEngine.Compute(
            data.Documents, data.TrackerRows, data.Project, query.ReportDate);

        return new EvmResponse(summary, data.RecalculationRequired, data.DocumentsWithoutSnapshot);
    }
}
