using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Application.Engine;

namespace Dip.Api.Features.ControlFindings.GetControlFindings;

// The four anomaly reports (PLAN.md § 5.4.4).
[Permission(Permissions.ReportsView)]
public sealed record GetControlFindingsQuery(Guid ProjectId) : IQuery<ControlFindingsResponse>;

public sealed record ControlFindingsResponse(
    Dip.Application.Engine.ControlFindings Findings,
    bool RecalculationRequired,
    int DocumentsWithoutSnapshot);
