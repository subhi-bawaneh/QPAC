using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Tracker.ListTrackerDocuments;

// The Tracker grid: server-paged because the project holds ~16k documents.
[Permission(Permissions.ReportsView)]
public sealed record ListTrackerDocumentsQuery(
    Guid ProjectId,
    string? Discipline = null,
    UnifiedStatus? Status = null,
    string? Search = null,
    bool? HasAconex = null,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<TrackerRowDto>>;
