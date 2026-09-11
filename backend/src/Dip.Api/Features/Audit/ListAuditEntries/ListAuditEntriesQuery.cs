using Dip.Api.Common;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Audit.ListAuditEntries;

// The audit log's reader. The refactor plan deleted rollback and named the audit log as
// its replacement, and until something displays it that was only half true: two handlers
// wrote to it and nothing read it.
//
// reports.view, not an admin permission: anyone who can see the register can see how it
// came to look the way it does.
[Permission(Permissions.ReportsView)]
public sealed record ListAuditEntriesQuery(
    Guid ProjectId,
    string? Entity = null,
    Guid? EntityId = null,
    int Page = 1,
    int PageSize = 50) : IQuery<PagedResult<AuditEntryDto>>;

public sealed record AuditEntryDto(
    Guid Id,
    string EntityName,
    Guid EntityId,
    string Action,
    string? Field,
    string? OldValue,
    string? NewValue,
    string UserId,
    DateTime At);
