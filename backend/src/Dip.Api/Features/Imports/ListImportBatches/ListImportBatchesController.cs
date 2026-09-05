using Dip.Api.Common;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Imports.ListImportBatches;

[Route("api/projects/{projectId:guid}/imports")]
[Authorize]
public sealed class ListImportBatchesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<ImportBatchSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<ImportBatchSummary>>> Get(
        Guid projectId,
        [FromQuery] ImportKind? kind,
        [FromQuery] Guid? folderFileId,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var result = await Dispatcher.Query(new ListImportBatchesQuery(projectId, kind, folderFileId, take), ct);
        return Ok(result);
    }
}
