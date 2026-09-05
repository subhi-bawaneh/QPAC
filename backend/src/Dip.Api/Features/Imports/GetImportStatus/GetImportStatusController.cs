using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Imports.GetImportStatus;

[Route("api/imports/{batchId:guid}/status")]
[Authorize]
public sealed class GetImportStatusController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ImportBatchSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportBatchSummary>> Get(Guid batchId, CancellationToken ct)
    {
        var result = await Dispatcher.Query(new GetImportStatusQuery(batchId), ct);
        return Ok(result);
    }
}
