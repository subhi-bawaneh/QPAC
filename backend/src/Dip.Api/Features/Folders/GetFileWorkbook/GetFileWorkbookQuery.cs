using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.GetFileWorkbook;

// The spreadsheet view of one file: the rows it produced, in the workbook's own
// column order, plus the Baseline and Picklists tabs the source workbook carries.
[Permission(Permissions.ReportsView)]
public sealed record GetFileWorkbookQuery(
    Guid FileId,
    string? Sheet = null,
    int Page = 1,
    int PageSize = 500) : IQuery<WorkbookDto>;
