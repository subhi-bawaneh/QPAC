using Dip.Api.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Features.Reports.ExportReport;

[Route("api/projects/{projectId:guid}/reports/{kind}/export")]
[Authorize]
public sealed class ExportReportController : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        Guid projectId, ReportKind kind, [FromQuery] DateTime? reportDate, CancellationToken ct)
    {
        var report = await Dispatcher.Query(new ExportReportQuery(projectId, kind, reportDate), ct);
        return File(report.Content, ExportedReport.ContentType, report.FileName);
    }
}
