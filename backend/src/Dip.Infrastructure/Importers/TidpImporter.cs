using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using ColMap = Dip.Infrastructure.Importers.DocumentRowParser.DocumentColumnMap;
using ExchangeRow = Dip.Infrastructure.Importers.DocumentRowParser.ExchangeRow;
using ParsedRow = Dip.Infrastructure.Importers.DocumentRowParser.ParsedRow;

namespace Dip.Infrastructure.Importers;

// Reads a TIDP workbook (`TIDP_Sheet`), extracts the discipline from the
// header block, then walks the DOCUMENT NUMBER table and upserts one Tidp
// row + one Document row per data row + two DataExchange rows per document.
//
// Target=Live writes to Tidp/Document/DataExchange directly.
// Target=Draft writes to TidpDraft/DocumentDraft/DataExchangeDraft and computes
// DraftRowState.New | Modified | Unchanged vs the current Live values, so the
// Promote diff (Phase 4.2) has everything it needs.
//
// Document numbering per PLAN.md § 5.1.1:
//   F01-F02-F03-F04-F05-F06-F07-F08A F08B F08C (no separator between S,T,U)
// Leading zeros preserved: Zone -> 2 chars, Sequence -> 4 chars.
public sealed class TidpImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    public TidpImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<ImportResult> ImportAsync(
        Guid projectId,
        string filePath,
        DataTarget target,
        Guid? folderFileId,
        Guid? importBatchId,
        string importedBy,
        CancellationToken ct)
    {
        using var wb = _reader.Open(filePath);
        if (!wb.TryGetSheet("TIDP_Sheet", out var sheet) || sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'TIDP_Sheet' sheet in workbook" });
        }

        var header = ReadHeaderBlock(sheet);
        var disciplineName = header.Discipline
            ?? throw new InvalidOperationException("Cannot find DISCIPLINE in TIDP header");

        var discipline = await ResolveDisciplineAsync(projectId, disciplineName, ct);
        var headerRow = DocumentRowParser.FindDocumentTableHeader(sheet);
        var columns = DocumentRowParser.MapDocumentColumns(sheet, headerRow);

        return target == DataTarget.Draft
            ? await ImportDraftAsync(sheet, headerRow, columns, projectId, discipline, header,
                folderFileId, importBatchId, importedBy, ct)
            : await ImportLiveAsync(sheet, headerRow, columns, projectId, discipline, header,
                folderFileId, importedBy, ct);
    }

    // ---------------------------------------------------------------- Live

    private async Task<ImportResult> ImportLiveAsync(
        IExcelSheet sheet, int headerRow, ColMap columns,
        Guid projectId, Discipline discipline, TidpHeader header,
        Guid? folderFileId, string importedBy, CancellationToken ct)
    {
        var tidp = await UpsertTidpAsync(projectId, discipline.Id, header, folderFileId, importedBy, ct);

        var existing = await _db.Documents
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.Id, d.DocumentNumber })
            .ToListAsync(ct);
        var existingByNumber = existing.ToDictionary(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase);

        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var parsed = DocumentRowParser.ParseRow(sheet, r, columns);
            if (parsed is null) continue;

            read++;

            if (existingByNumber.TryGetValue(parsed.DocumentNumber, out var existingRef))
            {
                var doc = await _db.Documents
                    .Include(d => d.Exchanges)
                    .SingleAsync(d => d.Id == existingRef.Id, ct);
                ApplyToDocument(doc, parsed, tidp.Id, discipline.Id, folderFileId, importedBy, isNew: false);
                updated++;
            }
            else
            {
                var doc = new Document();
                ApplyToDocument(doc, parsed, tidp.Id, discipline.Id, folderFileId, importedBy, isNew: true);
                doc.ProjectId = projectId;
                _db.Documents.Add(doc);
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }

    private static void ApplyToDocument(
        Document doc, ParsedRow parsed, Guid tidpId, Guid disciplineId, Guid? folderFileId,
        string importedBy, bool isNew)
    {
        doc.TidpId = tidpId;
        doc.DisciplineId = disciplineId;
        doc.FolderFileId = folderFileId;
        doc.DocumentNumber = parsed.DocumentNumber;
        doc.Title = parsed.Title;
        doc.ExtractedFromModel = parsed.ExtractedFromModel;
        doc.ScopeArea = parsed.ScopeArea;
        doc.AuthoringSoftware = parsed.AuthoringSoftware;
        doc.ExchangeFormat = parsed.ExchangeFormat;
        doc.Scale = parsed.Scale;
        doc.DeliveryMilestone = parsed.DeliveryMilestone;
        doc.PackageName = parsed.PackageName;
        doc.ActivityId = parsed.ActivityId;
        doc.ClassificationCode = parsed.ClassificationCode;
        doc.F01Project = parsed.F01;
        doc.F02Originator = parsed.F02;
        doc.F03Contract = parsed.F03;
        doc.F04DocType = parsed.F04;
        doc.F05Discipline = parsed.F05;
        doc.F06Zone = parsed.F06;
        doc.F07Building = parsed.F07;
        doc.F08ADrawingType = parsed.F08A;
        doc.F08BLevel = parsed.F08B;
        doc.F08CSequence = parsed.F08C;
        doc.CorporateDiscipline = parsed.CorporateDiscipline;
        // PLAN.md § 5.2 says BudgetWeight defaults to Exchange 01 duration (or 1).
        doc.BudgetWeight = parsed.Exchange1?.DurationDays ?? 1m;

        var now = DateTime.UtcNow;
        if (isNew)
        {
            doc.CreatedAt = now;
            doc.CreatedBy = importedBy;
        }
        doc.UpdatedAt = now;
        doc.UpdatedBy = importedBy;

        UpsertExchange(doc, 1, parsed.Exchange1);
        UpsertExchange(doc, 2, parsed.Exchange2);
    }

    private static void UpsertExchange(Document doc, int number, ExchangeRow? row)
    {
        var existing = doc.Exchanges.FirstOrDefault(e => e.Number == number);
        if (row is null)
        {
            if (existing is not null) doc.Exchanges.Remove(existing);
            return;
        }

        if (existing is null)
        {
            doc.Exchanges.Add(new DataExchange
            {
                Number = number,
                Author = row.Author,
                Geometrical = row.Geometrical,
                NonGeometrical = row.NonGeometrical,
                DurationDays = (int?)row.DurationDays,
                Predecessor = row.Predecessor,
                ExchangeDate = row.ExchangeDate,
            });
        }
        else
        {
            existing.Author = row.Author;
            existing.Geometrical = row.Geometrical;
            existing.NonGeometrical = row.NonGeometrical;
            existing.DurationDays = (int?)row.DurationDays;
            existing.Predecessor = row.Predecessor;
            existing.ExchangeDate = row.ExchangeDate;
        }
    }

    // --------------------------------------------------------------- Draft

    private async Task<ImportResult> ImportDraftAsync(
        IExcelSheet sheet, int headerRow, ColMap columns,
        Guid projectId, Discipline discipline, TidpHeader header,
        Guid? folderFileId, Guid? importBatchId, string importedBy, CancellationToken ct)
    {
        if (folderFileId is null || importBatchId is null)
        {
            throw new InvalidOperationException("Draft imports require FolderFileId and ImportBatchId");
        }

        // Clear any prior draft from the same FolderFile so the run is deterministic.
        await _db.DocumentDrafts
            .Where(d => d.FolderFileId == folderFileId.Value)
            .ExecuteDeleteAsync(ct);
        await _db.TidpDrafts
            .Where(d => d.FolderFileId == folderFileId.Value)
            .ExecuteDeleteAsync(ct);

        var tidpDraft = new TidpDraft
        {
            ProjectId = projectId,
            DisciplineId = discipline.Id,
            FolderFileId = folderFileId.Value,
            ImportBatchId = importBatchId.Value,
            DocumentReference = header.DocumentReference ?? string.Empty,
            RevisionNumber = header.RevisionNumber ?? "00",
            DateCreated = header.DateCreated,
            DateLastUpdated = header.DateLastUpdated,
            State = DraftRowState.New,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = importedBy,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = importedBy,
        };
        _db.TidpDrafts.Add(tidpDraft);

        // Preload existing Live docs so we can compute Diff state.
        var live = await _db.Documents
            .Include(d => d.Exchanges)
            .Where(d => d.ProjectId == projectId)
            .ToListAsync(ct);
        var liveByNumber = live.ToDictionary(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase);

        // Track duplicates within the same file.
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var parsed = DocumentRowParser.ParseRow(sheet, r, columns);
            if (parsed is null) continue;

            read++;

            var isDuplicate = !seen.Add(parsed.DocumentNumber);
            if (isDuplicate)
            {
                warnings.Add($"Row {r}: duplicate DocumentNumber {parsed.DocumentNumber} within this file");
            }

            var (state, liveDocId) = DiffAgainstLive(parsed, liveByNumber);

            var draft = new DocumentDraft
            {
                ProjectId = projectId,
                TidpDraftId = tidpDraft.Id,
                DisciplineId = discipline.Id,
                FolderFileId = folderFileId.Value,
                ImportBatchId = importBatchId.Value,
                DocumentNumber = parsed.DocumentNumber,
                Title = parsed.Title,
                ExtractedFromModel = parsed.ExtractedFromModel,
                ScopeArea = parsed.ScopeArea,
                AuthoringSoftware = parsed.AuthoringSoftware,
                ExchangeFormat = parsed.ExchangeFormat,
                Scale = parsed.Scale,
                DeliveryMilestone = parsed.DeliveryMilestone,
                PackageName = parsed.PackageName,
                ActivityId = parsed.ActivityId,
                ClassificationCode = parsed.ClassificationCode,
                F01Project = parsed.F01,
                F02Originator = parsed.F02,
                F03Contract = parsed.F03,
                F04DocType = parsed.F04,
                F05Discipline = parsed.F05,
                F06Zone = parsed.F06,
                F07Building = parsed.F07,
                F08ADrawingType = parsed.F08A,
                F08BLevel = parsed.F08B,
                F08CSequence = parsed.F08C,
                CorporateDiscipline = parsed.CorporateDiscipline,
                BudgetWeight = parsed.Exchange1?.DurationDays ?? 1m,
                State = state,
                LiveDocumentId = liveDocId,
                IsDuplicate = isDuplicate,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = importedBy,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = importedBy,
            };

            AppendDraftExchange(draft, 1, parsed.Exchange1);
            AppendDraftExchange(draft, 2, parsed.Exchange2);
            _db.DocumentDrafts.Add(draft);

            if (state == DraftRowState.New) inserted++;
            else if (state == DraftRowState.Modified) updated++;
            else skipped++;
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }

    private static void AppendDraftExchange(DocumentDraft draft, int number, ExchangeRow? row)
    {
        if (row is null) return;
        draft.Exchanges.Add(new DataExchangeDraft
        {
            Number = number,
            Author = row.Author,
            Geometrical = row.Geometrical,
            NonGeometrical = row.NonGeometrical,
            DurationDays = (int?)row.DurationDays,
            Predecessor = row.Predecessor,
            ExchangeDate = row.ExchangeDate,
        });
    }

    private static (DraftRowState State, Guid? LiveDocId) DiffAgainstLive(
        ParsedRow parsed, Dictionary<string, Document> liveByNumber)
    {
        if (!liveByNumber.TryGetValue(parsed.DocumentNumber, out var live))
        {
            return (DraftRowState.New, null);
        }

        // Field-by-field comparison lives in DraftDiff so the importers and the
        // Draft editor can never drift apart on what counts as "Modified".
        var state = DraftDiff.StateFor(DraftDiff.From(live), DocumentRowParser.ToComparable(parsed));
        return (state, live.Id);
    }

    // ---------------------------------------------------------------- Tidp

    private async Task<Tidp> UpsertTidpAsync(
        Guid projectId, Guid disciplineId, TidpHeader header,
        Guid? folderFileId, string importedBy, CancellationToken ct)
    {
        var existing = await _db.Tidps
            .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.DisciplineId == disciplineId, ct);

        var now = DateTime.UtcNow;
        if (existing is null)
        {
            var created = new Tidp
            {
                ProjectId = projectId,
                DisciplineId = disciplineId,
                FolderFileId = folderFileId,
                DocumentReference = header.DocumentReference ?? string.Empty,
                RevisionNumber = header.RevisionNumber ?? "00",
                DateCreated = header.DateCreated,
                DateLastUpdated = header.DateLastUpdated,
                CreatedAt = now,
                CreatedBy = importedBy,
                UpdatedAt = now,
                UpdatedBy = importedBy,
            };
            _db.Tidps.Add(created);
            await _db.SaveChangesAsync(ct);
            return created;
        }

        existing.FolderFileId = folderFileId ?? existing.FolderFileId;
        existing.DocumentReference = header.DocumentReference ?? existing.DocumentReference;
        existing.RevisionNumber = header.RevisionNumber ?? existing.RevisionNumber;
        existing.DateCreated = header.DateCreated ?? existing.DateCreated;
        existing.DateLastUpdated = header.DateLastUpdated ?? existing.DateLastUpdated;
        existing.UpdatedAt = now;
        existing.UpdatedBy = importedBy;
        return existing;
    }

    // -------------------------------------------------------------- Header

    private static TidpHeader ReadHeaderBlock(IExcelSheet sheet)
    {
        // Values sit in column B; labels in column A. We scan rows 1..14 and pick
        // out the ones we care about by label text.
        string? Get(string label)
        {
            var row = FindLabelRow(sheet, label);
            if (row is null) return null;
            return sheet.Row(row.Value).Cell(2).GetStringOrNull()?.Trim();
        }

        DateTime? GetDate(string label)
        {
            var row = FindLabelRow(sheet, label);
            if (row is null) return null;
            return sheet.Row(row.Value).Cell(2).GetDateTime();
        }

        return new TidpHeader(
            Client: Get("CLIENT"),
            Project: Get("PROJECT"),
            Organisation: Get("ORGANISATION"),
            Discipline: Get("DISCIPLINE"),
            Approver: Get("APPROVER"),
            DateCreated: GetDate("DATE CREATED"),
            DateLastUpdated: GetDate("DATE LAST UPDATED"),
            RevisionNumber: Get("REVISION NUMBER"),
            DocumentReference: Get("DOCUMENT REFERENCE"));
    }

    private static int? FindLabelRow(IExcelSheet sheet, string label)
    {
        var last = Math.Min(sheet.RowCount, 14);
        for (var r = 1; r <= last; r++)
        {
            var value = sheet.Row(r).Cell(1).GetStringOrNull()?.Trim();
            if (string.Equals(value, label, StringComparison.OrdinalIgnoreCase))
            {
                return r;
            }
        }
        return null;
    }

    private async Task<Discipline> ResolveDisciplineAsync(Guid projectId, string name, CancellationToken ct)
    {
        // First try by CorporateName (Structural, Electrical, Façade, …).
        var match = await _db.Disciplines
            .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.CorporateName == name, ct);
        if (match is not null) return match;

        // Fall back to Code (STL, STR, ARC, …).
        match = await _db.Disciplines
            .FirstOrDefaultAsync(d => d.ProjectId == projectId && d.Code == name, ct);
        if (match is not null) return match;

        // Create a placeholder Discipline row so import doesn't fail when a
        // workbook uses a discipline name the seeder didn't know about.
        var created = new Discipline
        {
            ProjectId = projectId,
            Code = name.Length > 20 ? name[..20] : name,
            CorporateName = name,
        };
        _db.Disciplines.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }

    // -------------------------------------------------- Value type helpers

    private sealed record TidpHeader(
        string? Client,
        string? Project,
        string? Organisation,
        string? Discipline,
        string? Approver,
        DateTime? DateCreated,
        DateTime? DateLastUpdated,
        string? RevisionNumber,
        string? DocumentReference);
}
