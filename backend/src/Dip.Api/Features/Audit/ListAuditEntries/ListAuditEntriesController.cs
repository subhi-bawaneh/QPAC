using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Audit.ListAuditEntries;

[Route("api/projects/{projectId:guid}/audit")]
[Authorize]
public sealed class ListAuditEntriesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditEntryDto>>> Get(
        Guid projectId,
        [FromQuery] string? entity,
        [FromQuery] Guid? entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(
            new ListAuditEntriesQuery(projectId, entity, entityId, page, pageSize), ct));
}
