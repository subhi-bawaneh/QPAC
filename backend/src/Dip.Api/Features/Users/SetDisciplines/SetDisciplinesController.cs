using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Users.SetDisciplines;

[Route("api/users/{id:guid}/disciplines")]
[Authorize]
public sealed class SetDisciplinesController : ApiControllerBase
{
    public sealed record SetDisciplinesRequest(string[] DisciplineCodes);

    [HttpPut]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Put(Guid id, [FromBody] SetDisciplinesRequest body, CancellationToken ct)
    {
        await Dispatcher.Send(new SetDisciplinesCommand(id, body.DisciplineCodes), ct);
        return NoContent();
    }
}
