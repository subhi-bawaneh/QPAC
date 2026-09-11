using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tidps.GetTidpFileGrid;

// The spreadsheet view of one uploaded TIDP file: the rows it produced, in the
// workbook's own column order, plus the Baseline and Picklists tabs the source
// workbook carried. Those two come from their tables, not from the file — the bytes
// are discarded after import.
[Permission(Permissions.ReportsView)]
public sealed record GetTidpFileGridQuery(
    Guid TidpFileId,
    string? Sheet = null,
    int Page = 1,
    int PageSize = 500) : IQuery<WorkbookDto>;
