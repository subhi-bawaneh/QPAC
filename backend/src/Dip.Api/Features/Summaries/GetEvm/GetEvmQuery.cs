using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Application.Engine;

namespace Dip.Api.Features.Summaries.GetEvm;

// Earned Value per discipline (PLAN.md § 7). CPI stays null until actual hours exist.
[Permission(Permissions.ReportsView)]
public sealed record GetEvmQuery(
    Guid ProjectId,
    DateTime? ReportDate = null) : IQuery<EvmResponse>;

public sealed record EvmResponse(
    EvmSummary Summary,
    bool RecalculationRequired,
    int DocumentsWithoutSnapshot);
