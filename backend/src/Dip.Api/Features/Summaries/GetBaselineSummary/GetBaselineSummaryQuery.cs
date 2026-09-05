using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Application.Engine;

namespace Dip.Api.Features.Summaries.GetBaselineSummary;

// The Baseline Summary (PLAN.md § 5.4.3): discipline rows, package rows and the
// packages-by-status rollup.
[Permission(Permissions.ReportsView)]
public sealed record GetBaselineSummaryQuery(Guid ProjectId) : IQuery<BaselineSummaryResponse>;

public sealed record BaselineSummaryResponse(
    BaselineSummary Summary,
    bool RecalculationRequired,
    int DocumentsWithoutSnapshot);
