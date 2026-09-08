using System.Globalization;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.GetFileWorkbook;

public sealed class GetFileWorkbookHandler : IQueryHandler<GetFileWorkbookQuery, WorkbookDto>
{
    public const string BaselineSheet = "Baseline";
    public const string PicklistsSheet = "Picklists";

    private const string DateFormat = "yyyy-MM-dd";

    private readonly DipDbContext _db;

    public GetFileWorkbookHandler(DipDbContext db) => _db = db;

    public async Task<WorkbookDto> Handle(GetFileWorkbookQuery query, CancellationToken ct)
    {
        var file = await _db.FolderFiles.AsNoTracking()
            .Include(f => f.Folder)
            .FirstOrDefaultAsync(f => f.Id == query.FileId && !f.IsDeleted, ct)
            ?? throw new KeyNotFoundException($"File {query.FileId} not found");

        var folder = file.Folder ?? throw new InvalidOperationException("File has no folder");
        var layer = folder.Target;
        var documentSheet = file.Kind == FileKind.Midp ? "MIDP" : "TIDP";

        // The three tabs are offered for every kind so they line up with the source
        // workbook, which carries all three.
        var sheets = new List<string> { documentSheet, BaselineSheet, PicklistsSheet };
        var requested = sheets.FirstOrDefault(
            s => string.Equals(s, query.Sheet, StringComparison.OrdinalIgnoreCase)) ?? documentSheet;

        var facts = await FolderProjections.LoadAsync(_db, folder.ProjectId, [file.Id], ct);
        var hasNewerDraft = facts.WithNewerDraft.Contains(file.Id);

        var sheet = requested switch
        {
            BaselineSheet => await BaselineSheetAsync(folder.ProjectId, query, ct),
            PicklistsSheet => await PicklistsSheetAsync(folder.ProjectId, query, ct),
            _ => await DocumentSheetAsync(file, layer, documentSheet, query, ct),
        };

        return new WorkbookDto(
            file.Id, file.Name, file.Kind, layer, file.ContentSource, file.ContentModifiedAt,
            hasNewerDraft, sheets, sheet);
    }

    // ------------------------------------------------------------ TIDP / MIDP

    private async Task<WorkbookSheetDto> DocumentSheetAsync(
        FolderFile file, DataTarget layer, string name, GetFileWorkbookQuery query, CancellationToken ct)
    {
        var skip = (Math.Max(query.Page, 1) - 1) * query.PageSize;

        if (layer == DataTarget.Draft)
        {
            var total = await _db.DocumentDrafts.CountAsync(d => d.FolderFileId == file.Id, ct);
            var drafts = await _db.DocumentDrafts.AsNoTracking()
                .Include(d => d.Exchanges)
                .Where(d => d.FolderFileId == file.Id)
                .OrderBy(d => d.DocumentNumber).ThenBy(d => d.Id)
                .Skip(skip).Take(query.PageSize)
                .ToListAsync(ct);

            var rows = drafts
                .Select((d, i) => new WorkbookRow(
                    d.Id, skip + i + 1,
                    Cells(d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea,
                        d.AuthoringSoftware, d.ExchangeFormat, d.Scale, d.DeliveryMilestone,
                        d.PackageName, d.ActivityId, d.ClassificationCode,
                        d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
                        d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
                        d.CorporateDiscipline,
                        Exchange(d.Exchanges, 1), Exchange(d.Exchanges, 2)),
                    d.State.ToString()))
                .ToList();

            return new WorkbookSheetDto(
                name, await DraftHeaderAsync(file.Id, ct), WorkbookColumns.Document,
                total, query.Page, query.PageSize, rows);
        }

        var liveTotal = await _db.Documents.CountAsync(d => d.FolderFileId == file.Id, ct);
        var documents = await _db.Documents.AsNoTracking()
            .Include(d => d.Exchanges)
            .Where(d => d.FolderFileId == file.Id)
            .OrderBy(d => d.DocumentNumber).ThenBy(d => d.Id)
            .Skip(skip).Take(query.PageSize)
            .ToListAsync(ct);

        var liveRows = documents
            .Select((d, i) => new WorkbookRow(
                d.Id, skip + i + 1,
                Cells(d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea,
                    d.AuthoringSoftware, d.ExchangeFormat, d.Scale, d.DeliveryMilestone,
                    d.PackageName, d.ActivityId, d.ClassificationCode,
                    d.F01Project, d.F02Originator, d.F03Contract, d.F04DocType, d.F05Discipline,
                    d.F06Zone, d.F07Building, d.F08ADrawingType, d.F08BLevel, d.F08CSequence,
                    d.CorporateDiscipline,
                    Exchange(d.Exchanges, 1), Exchange(d.Exchanges, 2)),
                null))
            .ToList();

        return new WorkbookSheetDto(
            name, await LiveHeaderAsync(file.Id, ct), WorkbookColumns.Document,
            liveTotal, query.Page, query.PageSize, liveRows);
    }

    // The nine label/value rows of the TIDP header block (PLAN.md § 5.1.1). A MIDP
    // workbook has no DISCIPLINE row, so the block is empty for it.
    private async Task<IReadOnlyList<WorkbookHeaderCell>> DraftHeaderAsync(Guid fileId, CancellationToken ct)
    {
        var draft = await _db.TidpDrafts.AsNoTracking()
            .FirstOrDefaultAsync(t => t.FolderFileId == fileId, ct);
        if (draft is null) return [];

        var discipline = await DisciplineNameAsync(draft.DisciplineId, ct);
        return await HeaderAsync(draft.ProjectId, discipline, draft.DateCreated, draft.DateLastUpdated,
            draft.RevisionNumber, draft.DocumentReference, ct);
    }

    private async Task<IReadOnlyList<WorkbookHeaderCell>> LiveHeaderAsync(Guid fileId, CancellationToken ct)
    {
        var tidp = await _db.Tidps.AsNoTracking()
            .FirstOrDefaultAsync(t => t.FolderFileId == fileId, ct);
        if (tidp is null) return [];

        var discipline = await DisciplineNameAsync(tidp.DisciplineId, ct);
        return await HeaderAsync(tidp.ProjectId, discipline, tidp.DateCreated, tidp.DateLastUpdated,
            tidp.RevisionNumber, tidp.DocumentReference, ct);
    }

    private Task<string?> DisciplineNameAsync(Guid disciplineId, CancellationToken ct) =>
        _db.Disciplines.AsNoTracking()
            .Where(d => d.Id == disciplineId)
            .Select(d => (string?)d.CorporateName)
            .FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyList<WorkbookHeaderCell>> HeaderAsync(
        Guid projectId, string? discipline, DateTime? created, DateTime? updated,
        string revision, string reference, CancellationToken ct)
    {
        var project = await _db.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId, ct);
        return
        [
            new("CLIENT", project?.Client),
            new("PROJECT", project?.Name),
            new("ORGANISATION", project?.Organisation),
            new("DISCIPLINE", discipline),
            new("APPROVER", project?.Approver),
            new("DATE CREATED", Format(created)),
            new("DATE LAST UPDATED", Format(updated)),
            new("REVISION NUMBER", revision),
            new("DOCUMENT REFERENCE", reference),
        ];
    }

    // ---------------------------------------------------------------- Baseline

    private async Task<WorkbookSheetDto> BaselineSheetAsync(
        Guid projectId, GetFileWorkbookQuery query, CancellationToken ct)
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
        Guid projectId, GetFileWorkbookQuery query, CancellationToken ct)
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

    private static string?[] Exchange(IEnumerable<DataExchangeDraft> exchanges, int number)
    {
        var e = exchanges.FirstOrDefault(x => x.Number == number);
        return e is null
            ? [null, null, null, null, null, null]
            : [e.Author, e.Geometrical, e.NonGeometrical,
               e.DurationDays?.ToString(CultureInfo.InvariantCulture), e.Predecessor,
               Format(e.ExchangeDate)];
    }

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
