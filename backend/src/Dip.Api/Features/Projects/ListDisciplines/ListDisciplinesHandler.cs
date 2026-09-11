using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Projects.ListDisciplines;

public sealed class ListDisciplinesHandler
    : IQueryHandler<ListDisciplinesQuery, IReadOnlyCollection<DisciplineDto>>
{
    private readonly DipDbContext _db;

    public ListDisciplinesHandler(DipDbContext db) => _db = db;

    public async Task<IReadOnlyCollection<DisciplineDto>> Handle(
        ListDisciplinesQuery query, CancellationToken ct) =>
        await _db.Disciplines
            .AsNoTracking()
            .Where(d => d.ProjectId == query.ProjectId)
            .OrderBy(d => d.CorporateName)
            .Select(d => new DisciplineDto(
                d.Id,
                d.Code,
                d.CorporateName,
                _db.TidpFiles.Count(t => t.DisciplineId == d.Id)))
            .ToListAsync(ct);
}
