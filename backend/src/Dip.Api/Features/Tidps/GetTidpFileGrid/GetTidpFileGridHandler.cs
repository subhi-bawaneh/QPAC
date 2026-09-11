using System.Globalization;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Tidps.GetTidpFileGrid;

public sealed class GetTidpFileGridHandler : IQueryHandler<GetTidpFileGridQuery, WorkbookDto>
{
    public const string BaselineSheet = "Baseline";
    public const string PicklistsSheet = "Picklists";
    public const string DocumentSheet = "TIDP";

    private const string DateFormat = "yyyy-MM-dd";

    private readonly DipDbContext _db;

    public GetTidpFileGridHandler(DipDbContext db) => _db = db;

    public async Task<WorkbookDto> Handle(GetTidpFileGridQuery query, CancellationToken ct)
    {
        var file = await _db.TidpFiles.AsNoTracking()
            .Include(f => f.Discipline)
            .FirstOrDefaultAsync(f => f.Id == query.TidpFileId, ct)
            ?? throw new KeyNotFoundException($"TIDP file {query.TidpFileId} not found");

        // The three tabs line up with the source workbook, which carried all three.
        var sheets = new List<string> { DocumentSheet, BaselineSheet, PicklistsSheet };
        var requested = sheets.FirstOrDefault(
            s => string.Equals(s, query.Sheet, StringComparison.OrdinalIgnoreCase)) ?? DocumentSheet;

        var sheet = requested switch
        {
            BaselineSheet => await BaselineSheetAsync(file.ProjectId, query, ct),
            PicklistsSheet => await PicklistsSheetAsync(file.ProjectId, query, ct),
            _ => await DocumentSheetAsync(file, query, ct),
        };

        return new WorkbookDto(
            file.Id, file.FileName, file.Discipline?.CorporateName ?? string.Empty,
            file.UploadedAt, sheets, sheet);
    }

    // ------------------------------------------------------------------- TIDP

    private async Task<WorkbookSheetDto> DocumentSheetAsync(
        TidpFile file, GetTidpFileGridQuery query, CancellationToken ct)
    {
        var skip = (Math.Max(query.Page, 1) - 1) * query.PageSize;

        var total = await _db.Documents.CountAsync(d => d.TidpFileId == file.Id, ct);
        var documents = await _db.Documents.AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => d.TidpFileId == file.Id)
            .OrderBy(d => d.DocumentNumber).ThenBy(d => d.Id)
            .Skip(skip).Take(query.PageSize)
            .ToListAsync(ct);

        var rows = documents
            .Select((d, i) => new WorkbookRow(
                d.Id, skip + i + 1,
                Cells(d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea,
                    d.AuthoringSoftware, d.ExchangeFormat, d.Scale, d.DeliveryMilestone,
                    d.PackageName, d.ActivityId, d.ClassificationCode,
                    d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
                    d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
                    d.CorporateDiscipline,
                    Exchange(d.Exchanges, 1), Exchange(d.Exchanges, 2)),
                // The grid marks a hand-edited row so imported data and typed data are
                // never mistaken for one another.
                d.IsEdited ? "Edited" : null))
            .ToList();

        return new WorkbookSheetDto(
            DocumentSheet, await HeaderAsync(file, ct), WorkbookColumns.Document,
            total, query.Page, query.PageSize, rows);
    }

    // The nine label/value rows of the TIDP header block (PLAN.md § 5.1.1).
    private async Task<IReadOnlyList<WorkbookHeaderCell>> HeaderAsync(
        TidpFile file, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == file.ProjectId, ct);

        return
        [
            new("CLIENT", project?.Client),
            new("PROJECT", project?.Name),
            new("ORGANISATION", project?.Organisation),
            new("DISCIPLINE", file.Discipline?.CorporateName),
            new("APPROVER", project?.Approver),
            new("DATE CREATED", Format(file.DateCreated)),
            new("DATE LAST UPDATED", Format(file.DateLastUpdated)),
            new("REVISION NUMBER", file.RevisionNumber),
            new("DOCUMENT REFERENCE", file.DocumentReference),
        ];
    }

    // ---------------------------------------------------------------- Baseline

    private async Task<WorkbookSheetDto> BaselineSheetAsync(
        Guid projectId, GetTidpFileGridQuery query, CancellationToken ct)
    {
        var skip = (Math.Max(query.Page, 1) - 1) * query.PageSize;
        var total = await _db.BaselineActivities.CountAsync(b => b.ProjectId == projectId, ct);
        var activities = await _db.BaselineActivities.AsNoTracking()
            .Where(b => b.ProjectId == projectId)
            .OrderBy(b => b.ActivityCode)
            .Skip(skip).Take(query.PageSize)
            .ToListAsync(ct);

        var rows = activities
            .Select((b, i) => new WorkbookRow(
                b.Id, skip + i + 1,
                new string?[]
                {
                    b.Package, b.ActivityCode, b.Type.ToString(),
                    b.OriginalDuration.ToString(CultureInfo.InvariantCulture),
                    Format(b.Start), Format(b.Finish),
                },
                null))
            .ToList();

        return new WorkbookSheetDto(
            BaselineSheet, [], WorkbookColumns.Baseline, total, query.Page, query.PageSize, rows);
    }

    // --------------------------------------------------------------- Picklists

    private async Task<WorkbookSheetDto> PicklistsSheetAsync(
        Guid projectId, GetTidpFileGridQuery query, CancellationToken ct)
    {
        var skip = (Math.Max(query.Page, 1) - 1) * query.PageSize;
        var total = await _db.PicklistItems.CountAsync(p => p.ProjectId == projectId && !p.IsDeleted, ct);
        var items = await _db.PicklistItems.AsNoTracking()
            .Where(p => p.ProjectId == projectId && !p.IsDeleted)
            .OrderBy(p => p.Field).ThenBy(p => p.SortOrder).ThenBy(p => p.Code)
            .Skip(skip).Take(query.PageSize)
            .ToListAsync(ct);

        var rows = items
            .Select((p, i) => new WorkbookRow(
                p.Id, skip + i + 1,
                new string?[]
                {
                    p.Field.ToString(), p.Code, p.Description,
                    p.SortOrder.ToString(CultureInfo.InvariantCulture),
                },
                null))
            .ToList();

        return new WorkbookSheetDto(
            PicklistsSheet, [], WorkbookColumns.Picklists, total, query.Page, query.PageSize, rows);
    }

    // ----------------------------------------------------------------- helpers

    private static string?[] Cells(
        string documentNumber, string title, string? extractedFromModel, string? scopeArea,
        string? authoringSoftware, string? exchangeFormat, string? scale, DateTime? deliveryMilestone,
        string? packageName, string? activityId, string? classificationCode,
        string f01, string f02, string f03, string f04, string f05,
        string f06, string f07, string f08a, string f08b, string f08c,
        string corporateDiscipline,
        string?[] exchange1, string?[] exchange2) =>
    [
        documentNumber, title, extractedFromModel, scopeArea, authoringSoftware, exchangeFormat,
        scale, Format(deliveryMilestone), packageName, activityId, classificationCode,
        f01, f02, f03, f04, f05, f06, f07, f08a, f08b, f08c, corporateDiscipline,
        .. exchange1, .. exchange2,
    ];

    private static string?[] Exchange(IEnumerable<DataExchange> exchanges, int number)
    {
        var e = exchanges.FirstOrDefault(x => x.Number == number);
        return e is null
            ? [null, null, null, null, null, null]
            : [e.Author, e.Geometrical, e.NonGeometrical,
               e.DurationDays?.ToString(CultureInfo.InvariantCulture), e.Predecessor,
               Format(e.ExchangeDate)];
    }

    private static string? Format(DateTime? value) =>
        value?.ToString(DateFormat, CultureInfo.InvariantCulture);
}
