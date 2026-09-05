using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Baseline.ListBaselineActivities;

// The baseline programme, paged. `Used` is the sheet's own column: does any document
// point at this activity code (docs/excel-analysis.md § 5.1.4)?
[Permission(Permissions.ReportsView)]
public sealed record ListBaselineActivitiesQuery(
    Guid ProjectId,
    BaselineActivityType? Type = null,
    bool? Used = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<BaselineActivityDto>>;

public sealed record BaselineActivityDto(
    Guid Id,
    string ActivityCode,
    string Package,
    BaselineActivityType Type,
    int OriginalDuration,
    DateTime Start,
    DateTime Finish,
    int DocumentCount,
    bool Used);
