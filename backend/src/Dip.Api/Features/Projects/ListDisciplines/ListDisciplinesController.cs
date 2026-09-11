using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Projects.ListDisciplines;

[Route("api/projects/{projectId:guid}/disciplines")]
[Authorize]
public sealed class ListDisciplinesController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<DisciplineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyCollection<DisciplineDto>>> Get(
        Guid projectId, CancellationToken ct = default) =>
        Ok(await Dispatcher.Query(new ListDisciplinesQuery(projectId), ct));
}
