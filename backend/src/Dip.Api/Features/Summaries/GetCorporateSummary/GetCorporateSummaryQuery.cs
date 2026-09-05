using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Application.Engine;

namespace Dip.Api.Features.Summaries.GetCorporateSummary;

// The Corporate Summary (PLAN.md § 5.4.2). ReportDate defaults to the project's
// setting, then to today; passing one lets the UI look at any week.
[Permission(Permissions.ReportsView)]
public sealed record GetCorporateSummaryQuery(
    Guid ProjectId,
    DateTime? ReportDate = null) : IQuery<CorporateSummaryResponse>;

public sealed record CorporateSummaryResponse(
    CorporateSummary Summary,
    bool RecalculationRequired,
    int DocumentsWithoutSnapshot);
