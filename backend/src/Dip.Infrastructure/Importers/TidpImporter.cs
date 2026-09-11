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

// Reads a TIDP workbook (`TIDP_Sheet`), extracts the discipline from the header
// block, then walks the DOCUMENT NUMBER table and writes one Document row per
// data row plus two DataExchange rows per document, all owned by the TidpFile
// the upload created.
//
// Ownership is first-file-wins. A number another TidpFile already owns is skipped
// with a counted warning naming both files, never stolen: otherwise re-uploading
// one discipline would silently move a document out of another.
//
// The number of record is the sheet's own DOCUMENT NUMBER, normalised. The
// recompose from F01..F08C is a cross-check that becomes a warning here and a
// control finding in stage 4 — never a rewrite.
public sealed class TidpImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    public TidpImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    // Parses the workbook's discipline without importing anything, so the upload
    // slice can create the TidpFile row before the rows land under it.
    public string ReadDiscipline(Stream content)
    {
        using var wb = _reader.Open(content);
        if (!wb.TryGetSheet("TIDP_Sheet", out var sheet) || sheet is null)
        {
            throw new InvalidOperationException("No 'TIDP_Sheet' sheet in workbook");
        }

        return ReadHeaderBlock(sheet).Discipline
            ?? throw new InvalidOperationException("Cannot find DISCIPLINE in TIDP header");
    }

    public async Task<ImportResult> ImportAsync(
        Guid projectId,
        Stream content,
        Guid tidpFileId,
        string importedBy,
        CancellationToken ct)
    {
        using var wb = _reader.Open(content);
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

        // Sequence widths are data (DocumentTypeSerials), read once per import.
        var widths = SerialWidths.Create(
            await _db.DocumentTypeSerials.Where(s => s.ProjectId == projectId && !s.IsDeleted).ToListAsync(ct));

        var project = await _db.Projects.FirstAsync(p => p.Id == projectId, ct);
        var file = await _db.TidpFiles.FirstAsync(t => t.Id == tidpFileId, ct);

        file.DisciplineId = discipline.Id;
        file.DocumentReference = header.DocumentReference ?? string.Empty;
        file.RevisionNumber = header.RevisionNumber ?? "00";
        file.DateCreated = header.DateCreated;
        file.DateLastUpdated = header.DateLastUpdated;

        // Every document number in the project and who owns it, so an incoming row
        // can be matched to this file's own row or refused as another file's.
        var existing = await _db.Documents
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.Id, d.DocumentNumber, d.TidpFileId })
            .ToListAsync(ct);
        var existingByNumber = existing.ToDictionary(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase);

        var ownerNames = await _db.TidpFiles
            .Where(t => t.ProjectId == projectId)
            .Select(t => new { t.Id, t.FileName })
            .ToDictionaryAsync(t => t.Id, t => t.FileName, ct);

        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();
        var seenInThisFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var parsed = DocumentRowParser.ParseRow(sheet, r, columns, widths);
            if (parsed is null)
            {
                // A row below the last populated one is the template's tail, not a
                // defect. A row that has content but no F01/F04/F05 is a defect, and
                // DocumentRowParser reports which through Rejection.
                continue;
            }

            read++;

            // A row carrying another project's code is an Excel autofill accident,
            // not a document. Rejected with the value so it can be found and fixed.
            if (!string.Equals(parsed.F01, project.Code, StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                warnings.Add(
                    $"Row {r}: PROJECT is {parsed.F01}, not {project.Code} - rejected ({parsed.DocumentNumber})");
                continue;
            }

            if (parsed.Mismatch is { } mismatch)
            {
                warnings.Add(
                    $"Row {r}: document number does not match its fields: "
                    + $"{mismatch.Field} is {mismatch.FieldSays}, number says {mismatch.NumberSays}");
            }

            if (!seenInThisFile.Add(parsed.DocumentNumber))
            {
                skipped++;
                warnings.Add($"Row {r}: {parsed.DocumentNumber} appears more than once in this file - skipped");
                continue;
            }

            if (existingByNumber.TryGetValue(parsed.DocumentNumber, out var existingRef))
            {
                if (existingRef.TidpFileId != tidpFileId)
                {
                    var owner = ownerNames.TryGetValue(existingRef.TidpFileId, out var name) ? name : "another file";
                    skipped++;
                    warnings.Add(
                        $"Row {r}: {parsed.DocumentNumber} already belongs to {owner} - skipped");
                    continue;
                }

                var doc = await _db.Documents
                    .Include(d => d.Exchanges)
                    .SingleAsync(d => d.Id == existingRef.Id, ct);
                ApplyToDocument(doc, parsed, tidpFileId, discipline.Id, importedBy, isNew: false);
                updated++;
            }
            else
            {
                var doc = new Document();
                ApplyToDocument(doc, parsed, tidpFileId, discipline.Id, importedBy, isNew: true);
                doc.ProjectId = projectId;
                _db.Documents.Add(doc);
                inserted++;
            }
        }

        file.RowsRead = read;
        file.RowsImported = inserted + updated;
        file.RowsSkipped = skipped;

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }

    private static void ApplyToDocument(
        Document doc, ParsedRow parsed, Guid tidpFileId, Guid disciplineId,
        string importedBy, bool isNew)
    {
        doc.TidpFileId = tidpFileId;
        doc.DisciplineId = disciplineId;
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

        // An import is not a hand edit: a replaced row goes back to being imported
        // data, which is what the dialog's edited-row count has already warned about.
        doc.IsEdited = false;
        doc.EditedBy = null;
        doc.EditedAt = null;

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
