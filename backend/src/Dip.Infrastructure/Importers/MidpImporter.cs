using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Master MIDP importer. Same A..AH schema as TIDP, but:
//   1. Header block has no DISCIPLINE row - discipline varies per-row (col V,
//      CORPORATE DISCIPLINE).
//   2. ~15,885 populated rows in the sample - inserts happen in 2,000-row
//      chunks so EF's change tracker doesn't drown.
//   3. IsDuplicate is flagged when a DocumentNumber appears twice within the
//      same file. First occurrence is kept; the rest carry IsDuplicate=true.
//   4. One MIDP Tidp per import batch (all disciplines share it); Documents'
//      DisciplineId points to the actual per-row discipline row.
public sealed class MidpImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    private const int InsertChunkSize = 2000;
    private const string MidpDocumentReference = "MIDP-MASTER";

    public MidpImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<ImportResult> ImportAsync(
        Guid projectId,
        Stream content,
        DataTarget target,
        Guid? folderFileId,
        Guid? importBatchId,
        string importedBy,
        CancellationToken ct)
    {
        using var wb = _reader.Open(content);
        if (!wb.TryGetSheet("MIDP", out var sheet) || sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'MIDP' sheet in workbook" });
        }

        var headerRow = DocumentRowParser.FindDocumentTableHeader(sheet);
        var columns = DocumentRowParser.MapDocumentColumns(sheet, headerRow);

        return target == DataTarget.Draft
            ? await ImportDraftAsync(sheet, headerRow, columns, projectId,
                folderFileId, importBatchId, importedBy, ct)
            : await ImportLiveAsync(sheet, headerRow, columns, projectId,
                folderFileId, importedBy, ct);
    }

    // ---------------------------------------------------------------- Live

    private async Task<ImportResult> ImportLiveAsync(
        IExcelSheet sheet, int headerRow, DocumentRowParser.DocumentColumnMap columns,
        Guid projectId, Guid? folderFileId, string importedBy, CancellationToken ct)
    {
        // Preload existing Disciplines and Documents in one query each.
        var disciplineCache = await LoadDisciplineCacheAsync(projectId, ct);

        var existing = await _db.Documents
            .Where(d => d.ProjectId == projectId)
            .Select(d => new { d.Id, d.DocumentNumber })
            .ToListAsync(ct);
        var existingByNumber = existing.ToDictionary(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase);

        // Ensure a single "MIDP Master" Tidp exists as the anchor for every row.
        // Use the seed's default discipline (Structural) so we satisfy the FK;
        // per-row discipline lives on Document.DisciplineId anyway.
        var anchorDiscipline = disciplineCache.Values.FirstOrDefault()
            ?? throw new InvalidOperationException("No Disciplines seeded for the project");
        var tidp = await UpsertMasterTidpAsync(projectId, anchorDiscipline.Id, folderFileId, importedBy, ct);

        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var duplicates = 0;
        var warnings = new List<string>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<Document>(InsertChunkSize);

        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var parsed = DocumentRowParser.ParseRow(sheet, r, columns);
            if (parsed is null) continue;

            read++;

            if (string.IsNullOrEmpty(parsed.CorporateDiscipline))
            {
                warnings.Add($"Row {r}: CORPORATE DISCIPLINE is blank - skipped");
                skipped++;
                continue;
            }

            var discipline = await GetOrCreateDisciplineAsync(disciplineCache, projectId, parsed.CorporateDiscipline, ct);
            var isDuplicate = !seenInFile.Add(parsed.DocumentNumber);
            if (isDuplicate)
            {
                // Duplicates within the same file are counted + warned but not written
                // (the UNIQUE index on ProjectId+DocumentNumber would reject them anyway).
                duplicates++;
                skipped++;
                continue;
            }

            if (existingByNumber.TryGetValue(parsed.DocumentNumber, out var existingRef))
            {
                // Attach then update. Save happens in the chunk flush below.
                var doc = new Document { Id = existingRef.Id };
                _db.Documents.Attach(doc);
                ApplyLiveFields(doc, parsed, tidp.Id, discipline.Id, folderFileId, importedBy, isNew: false);
                updated++;
                await MaybeFlushAsync(pending, ct);
            }
            else
            {
                var doc = new Document();
                ApplyLiveFields(doc, parsed, tidp.Id, discipline.Id, folderFileId, importedBy, isNew: true);
                doc.ProjectId = projectId;
                pending.Add(doc);
                inserted++;
                if (pending.Count >= InsertChunkSize)
                {
                    await FlushInsertsAsync(pending, ct);
                }
            }
        }

        await FlushInsertsAsync(pending, ct);
        await _db.SaveChangesAsync(ct);
        if (duplicates > 0)
        {
            warnings.Insert(0, $"{duplicates} duplicate DocumentNumber(s) detected within the file");
        }
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }

    private async Task MaybeFlushAsync(List<Document> pending, CancellationToken ct)
    {
        if (pending.Count == 0) return;
        await _db.SaveChangesAsync(ct);
    }

    private async Task FlushInsertsAsync(List<Document> pending, CancellationToken ct)
    {
        if (pending.Count == 0) return;
        _db.Documents.AddRange(pending);
        await _db.SaveChangesAsync(ct);
        // Detach the batch so the change tracker doesn't keep growing.
        foreach (var doc in pending)
        {
            _db.Entry(doc).State = EntityState.Detached;
            foreach (var ex in doc.Exchanges)
            {
                _db.Entry(ex).State = EntityState.Detached;
            }
        }
        pending.Clear();
    }

    private static void ApplyLiveFields(
        Document doc, DocumentRowParser.ParsedRow parsed, Guid tidpId, Guid disciplineId,
        Guid? folderFileId, string importedBy, bool isNew)
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
        doc.BudgetWeight = parsed.Exchange1?.DurationDays ?? 1m;

        var now = DateTime.UtcNow;
        if (isNew)
        {
            doc.CreatedAt = now;
            doc.CreatedBy = importedBy;
        }
        doc.UpdatedAt = now;
        doc.UpdatedBy = importedBy;

        AttachExchange(doc, 1, parsed.Exchange1);
        AttachExchange(doc, 2, parsed.Exchange2);
    }

    private static void AttachExchange(Document doc, int number, DocumentRowParser.ExchangeRow? row)
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
        IExcelSheet sheet, int headerRow, DocumentRowParser.DocumentColumnMap columns,
        Guid projectId, Guid? folderFileId, Guid? importBatchId, string importedBy, CancellationToken ct)
    {
        if (folderFileId is null || importBatchId is null)
        {
            throw new InvalidOperationException("Draft imports require FolderFileId and ImportBatchId");
        }

        await _db.DocumentDrafts.Where(d => d.FolderFileId == folderFileId.Value).ExecuteDeleteAsync(ct);
        await _db.TidpDrafts.Where(d => d.FolderFileId == folderFileId.Value).ExecuteDeleteAsync(ct);

        var disciplineCache = await LoadDisciplineCacheAsync(projectId, ct);
        var anchorDiscipline = disciplineCache.Values.First();

        var tidpDraft = new TidpDraft
        {
            ProjectId = projectId,
            DisciplineId = anchorDiscipline.Id,
            FolderFileId = folderFileId.Value,
            ImportBatchId = importBatchId.Value,
            DocumentReference = MidpDocumentReference,
            RevisionNumber = "00",
            State = DraftRowState.New,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = importedBy,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = importedBy,
        };
        _db.TidpDrafts.Add(tidpDraft);
        await _db.SaveChangesAsync(ct);

        // Preload Live for diff. For 15k rows this is fine (single query, dictionary lookup).
        var live = await _db.Documents
            .Where(d => d.ProjectId == projectId)
            .Select(d => new LiveDocSnapshot(
                d.Id, d.DocumentNumber, d.Title, d.ExtractedFromModel, d.ScopeArea, d.PackageName,
                d.ActivityId, d.ClassificationCode, d.CorporateDiscipline, d.DeliveryMilestone,
                d.Scale, d.AuthoringSoftware, d.ExchangeFormat, d.BudgetWeight))
            .ToListAsync(ct);
        var liveByNumber = live.ToDictionary(d => d.DocumentNumber, StringComparer.OrdinalIgnoreCase);

        var read = 0;
        var newCount = 0;
        var modifiedCount = 0;
        var unchangedCount = 0;
        var skipped = 0;
        var warnings = new List<string>();
        var seenInFile = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pending = new List<DocumentDraft>(InsertChunkSize);

        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var parsed = DocumentRowParser.ParseRow(sheet, r, columns);
            if (parsed is null) continue;

            read++;

            if (string.IsNullOrEmpty(parsed.CorporateDiscipline))
            {
                warnings.Add($"Row {r}: CORPORATE DISCIPLINE blank - skipped");
                skipped++;
                continue;
            }

            var discipline = await GetOrCreateDisciplineAsync(disciplineCache, projectId, parsed.CorporateDiscipline, ct);
            var isDuplicate = !seenInFile.Add(parsed.DocumentNumber);

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
            pending.Add(draft);

            if (state == DraftRowState.New) newCount++;
            else if (state == DraftRowState.Modified) modifiedCount++;
            else unchangedCount++;

            if (pending.Count >= InsertChunkSize)
            {
                _db.DocumentDrafts.AddRange(pending);
                await _db.SaveChangesAsync(ct);
                foreach (var p in pending)
                {
                    _db.Entry(p).State = EntityState.Detached;
                    foreach (var ex in p.Exchanges) _db.Entry(ex).State = EntityState.Detached;
                }
                pending.Clear();
            }
        }

        if (pending.Count > 0)
        {
            _db.DocumentDrafts.AddRange(pending);
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResult(read, newCount, modifiedCount, skipped + unchangedCount, warnings);
    }

    private static (DraftRowState State, Guid? LiveDocId) DiffAgainstLive(
        DocumentRowParser.ParsedRow parsed,
        Dictionary<string, LiveDocSnapshot> liveByNumber)
    {
        if (!liveByNumber.TryGetValue(parsed.DocumentNumber, out var live))
        {
            return (DraftRowState.New, null);
        }

        var state = DraftDiff.StateFor(live.Fields, DocumentRowParser.ToComparable(parsed));
        return (state, live.Id);
    }

    // Projection used only for the Draft diff so we avoid loading full Document graphs.
    private sealed record LiveDocSnapshot(
        Guid Id, string DocumentNumber, string Title, string? ExtractedFromModel, string? ScopeArea,
        string? PackageName, string? ActivityId, string? ClassificationCode, string CorporateDiscipline,
        DateTime? DeliveryMilestone, string? Scale, string? AuthoringSoftware, string? ExchangeFormat,
        decimal BudgetWeight)
    {
        // Computed in memory — EF only projects the constructor columns above.
        public DraftComparableFields Fields => new(
            Title, ExtractedFromModel, ScopeArea, PackageName, ActivityId, ClassificationCode,
            CorporateDiscipline, DeliveryMilestone, Scale, AuthoringSoftware, ExchangeFormat,
            BudgetWeight);
    }

    private static void AppendDraftExchange(DocumentDraft draft, int number, DocumentRowParser.ExchangeRow? row)
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

    // ---------------------------------------------------- Discipline cache

    private async Task<Dictionary<string, Discipline>> LoadDisciplineCacheAsync(Guid projectId, CancellationToken ct)
    {
        var rows = await _db.Disciplines
            .Where(d => d.ProjectId == projectId)
            .ToListAsync(ct);
        // Index by CorporateName for the primary lookup; Code lookup falls back.
        var byName = new Dictionary<string, Discipline>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in rows)
        {
            byName.TryAdd(d.CorporateName, d);
            byName.TryAdd(d.Code, d);
        }
        return byName;
    }

    private async Task<Discipline> GetOrCreateDisciplineAsync(
        Dictionary<string, Discipline> cache, Guid projectId, string name, CancellationToken ct)
    {
        if (cache.TryGetValue(name, out var existing))
        {
            return existing;
        }

        var created = new Discipline
        {
            ProjectId = projectId,
            Code = name.Length > 20 ? name[..20] : name,
            CorporateName = name,
        };
        _db.Disciplines.Add(created);
        await _db.SaveChangesAsync(ct);
        cache[created.CorporateName] = created;
        cache[created.Code] = created;
        return created;
    }

    private async Task<Tidp> UpsertMasterTidpAsync(
        Guid projectId, Guid disciplineId, Guid? folderFileId, string importedBy, CancellationToken ct)
    {
        var existing = await _db.Tidps
            .FirstOrDefaultAsync(t => t.ProjectId == projectId && t.DocumentReference == MidpDocumentReference, ct);
        var now = DateTime.UtcNow;
        if (existing is not null)
        {
            existing.FolderFileId = folderFileId ?? existing.FolderFileId;
            existing.UpdatedAt = now;
            existing.UpdatedBy = importedBy;
            return existing;
        }

        var created = new Tidp
        {
            ProjectId = projectId,
            DisciplineId = disciplineId,
            FolderFileId = folderFileId,
            DocumentReference = MidpDocumentReference,
            RevisionNumber = "00",
            CreatedAt = now,
            CreatedBy = importedBy,
            UpdatedAt = now,
            UpdatedBy = importedBy,
        };
        _db.Tidps.Add(created);
        await _db.SaveChangesAsync(ct);
        return created;
    }
}
