using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Lists.GetStatusMappings;

// The Aconex status -> unified status table (PLAN.md § 5.3). Legacy rows are kept
// and flagged: older exports still carry those spellings.
[Permission(Permissions.ReportsView)]
public sealed record GetStatusMappingsQuery(Guid ProjectId) : IQuery<IReadOnlyList<StatusMappingDto>>;

public sealed record StatusMappingDto(Guid Id, string AconexStatus, UnifiedStatus Status, bool IsLegacy);
