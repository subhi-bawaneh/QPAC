using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Projects.GetProjectSettings;

public sealed class GetProjectSettingsHandler
    : IQueryHandler<GetProjectSettingsQuery, ProjectSettingsDto>
{
    private readonly DipDbContext _db;

    public GetProjectSettingsHandler(DipDbContext db) => _db = db;

    public async Task<ProjectSettingsDto> Handle(GetProjectSettingsQuery query, CancellationToken ct)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == query.ProjectId, ct)
            ?? throw new KeyNotFoundException($"Project {query.ProjectId} not found");

        return new ProjectSettingsDto(
            project.Id, project.Code, project.Name, project.Client, project.Organisation,
            project.Approver, project.ScheduleMode, project.WorkingPlanApprovalDays,
            project.ReportDate, project.BaselineStartDate,
            project.WeightPending, project.WeightSub1, project.WeightSub2, project.WeightApproved);
    }
}
