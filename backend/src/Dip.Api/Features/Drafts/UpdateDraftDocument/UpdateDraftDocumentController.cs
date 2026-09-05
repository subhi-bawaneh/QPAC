using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Drafts.UpdateDraftDocument;

[Route("api/drafts/documents/{id:guid}")]
[Authorize]
public sealed class UpdateDraftDocumentController : ApiControllerBase
{
    public sealed record UpdateDraftDocumentRequest(
        string Title,
        string? ExtractedFromModel,
        string? ScopeArea,
        string? AuthoringSoftware,
        string? ExchangeFormat,
        string? Scale,
        DateTime? DeliveryMilestone,
        string? PackageName,
        string? ActivityId,
        string? ClassificationCode,
        string F01Project,
        string F02Originator,
        string F03Contract,
        string F04DocType,
        string F05Discipline,
        string F06Zone,
        string F07Building,
        string F08ADrawingType,
        string F08BLevel,
        string F08CSequence,
        string CorporateDiscipline,
        IReadOnlyList<DraftExchangeInput>? Exchanges);

    [HttpPut]
    [ProducesResponseType(typeof(DraftDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DraftDocumentDto>> Put(
        Guid id, [FromBody] UpdateDraftDocumentRequest body, CancellationToken ct)
    {
        var result = await Dispatcher.Send(new UpdateDraftDocumentCommand(
            id, body.Title, body.ExtractedFromModel, body.ScopeArea, body.AuthoringSoftware,
            body.ExchangeFormat, body.Scale, body.DeliveryMilestone, body.PackageName,
            body.ActivityId, body.ClassificationCode,
            body.F01Project, body.F02Originator, body.F03Contract, body.F04DocType,
            body.F05Discipline, body.F06Zone, body.F07Building,
            body.F08ADrawingType, body.F08BLevel, body.F08CSequence,
            body.CorporateDiscipline, body.Exchanges), ct);
        return Ok(result);
    }
}
