using System.Text.RegularExpressions;
using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Reads the Aconex History export (~25,246 rows) and materialises one
// AconexRevision per row with:
//   - DocNoFinal: whitespace-stripped, repeated hyphens collapsed, anything past the
//     eighth segment dropped. Null when what is left is not an 8-segment number.
//   - IsTerminated: a same-DocNo + same-Revision row at or after this row
//     has ReviewStatus="Terminated" OR Status in ("Closed", "No Longer In Use").
//   - IsLatest: this row has the max DateModified for its DocNoFinal.
//
// An upload APPENDS. It never deletes: the owner exports periodically and cannot
// remember what was already loaded, so a line already held is dropped and counted and
// everything else is inserted. Identity is AconexLineHasher over the whole normalised
// line — see there for why it is not (document, revision, date).
//
// Both flags are cross-row aggregates, so they are recomputed over the WHOLE surviving
// set after an append, never over the new rows alone: otherwise a drawing that gains a
// later event keeps an older row still claiming to be the latest, and the tracker picks
// between them arbitrarily.
public sealed class AconexHistoryImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    private const int InsertChunkSize = 2000;
    private const int SegmentCount = 8;

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex HyphenRunRegex = new("-{2,}", RegexOptions.Compiled);
    private static readonly HashSet<string> TerminalReviewStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Terminated",
    };
    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Closed",
        "No Longer In Use",
        "No Longer in Use", // legacy casing, defensive
    };

    public AconexHistoryImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    // The hash of every line in a workbook, without writing anything. The preview needs
    // exactly this and nothing else: how many of these lines the project already holds.
    public IReadOnlyList<string> ReadLineHashes(Stream content)
    {
        using var wb = _reader.Open(content);
        var sheet = FindSheet(wb)
            ?? throw new InvalidOperationException("No 'Aconex History' or 'SHD_History' sheet");

        var headerRow = FindHeaderRow(sheet)
            ?? throw new InvalidOperationException("Cannot locate 'Document No' header in Aconex sheet");
        var cols = MapColumns(sheet, headerRow);

        var hashes = new List<string>(capacity: 30_000);
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var rawDocNo = row.Cell(cols.DocumentNo).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(rawDocNo)) continue;

            var dateModified = row.Cell(cols.DateModified).GetDateTime();
            if (dateModified is null) continue;

            hashes.Add(HashOf(row, cols, rawDocNo, dateModified.Value));
        }

        return hashes;
    }

    private static IExcelSheet? FindSheet(IExcelWorkbook wb)
    {
        foreach (var candidate in new[] { "Aconex History", "SHD_History" })
        {
            if (wb.TryGetSheet(candidate, out var sheet) && sheet is not null) return sheet;
        }
        return null;
    }

    private static string HashOf(IExcelRow row, ColumnMap cols, string rawDocNo, DateTime dateModified) =>
        AconexLineHasher.Compute(
            row.Cell(cols.File).GetStringOrNull(),
            row.Cell(cols.FileName).GetStringOrNull(),
            rawDocNo,
            row.Cell(cols.Revision).GetStringOrNull(),
            row.Cell(cols.Title).GetStringOrNull(),
            row.Cell(cols.Status).GetStringOrNull(),
            row.Cell(cols.ReviewStatus).GetStringOrNull(),
            dateModified,
            row.Cell(cols.Type).GetStringOrNull(),
            row.Cell(cols.Discipline).GetStringOrNull(),
            row.Cell(cols.Area).GetStringOrNull(),
            row.Cell(cols.Venue).GetStringOrNull(),
            row.Cell(cols.FloorLevel).GetStringOrNull(),
            row.Cell(cols.TransmittalIn).GetStringOrNull());

    public async Task<ImportResult> ImportAsync(
        Guid projectId,
        Stream content,
        Guid importBatchId,
        string importedBy,
        CancellationToken ct)
    {
        _ = importedBy;

        using var wb = _reader.Open(content);
        // Different workbooks use different sheet names for the same data:
        //   MIDP.xlsx     -> "Aconex History"
        //   Tracker.xlsx  -> "SHD_History"
        IExcelSheet? sheet = null;
        foreach (var candidate in new[] { "Aconex History", "SHD_History" })
        {
            if (wb.TryGetSheet(candidate, out sheet) && sheet is not null) break;
        }
        if (sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'Aconex History' or 'SHD_History' sheet" });
        }

        var headerRow = FindHeaderRow(sheet)
            ?? throw new InvalidOperationException("Cannot locate 'Document No' header in Aconex sheet");
        var cols = MapColumns(sheet, headerRow);

        // Pass 1: parse every row into memory (25k rows -> ~10 MB, fine on IIS).
        var parsed = new List<ParsedRevision>(capacity: 30_000);
        var warnings = new List<string>();
        var skipped = 0;

        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var rawDocNo = row.Cell(cols.DocumentNo).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(rawDocNo))
            {
                continue;
            }

            var dateModified = row.Cell(cols.DateModified).GetDateTime();
            if (dateModified is null)
            {
                warnings.Add($"Row {r}: missing Date Modified - skipped");
                skipped++;
                continue;
            }

            var lineHash = HashOf(row, cols, rawDocNo, dateModified.Value);

            parsed.Add(new ParsedRevision(
                RowNumber: r,
                LineHash: lineHash,
                FileType: row.Cell(cols.File).GetStringOrNull()?.Trim() ?? string.Empty,
                FileName: row.Cell(cols.FileName).GetStringOrNull()?.Trim() ?? string.Empty,
                AconexDocNo: rawDocNo,
                DocNoFinal: NormalizeDocNo(rawDocNo),
                Revision: (row.Cell(cols.Revision).GetStringOrNull()?.Trim() ?? "00").PadLeft(2, '0'),
                Title: row.Cell(cols.Title).GetStringOrNull()?.Trim() ?? string.Empty,
                Status: row.Cell(cols.Status).GetStringOrNull()?.Trim() ?? string.Empty,
                ReviewStatus: row.Cell(cols.ReviewStatus).GetStringOrNull()?.Trim(),
                DateModified: DateTime.SpecifyKind(dateModified.Value, DateTimeKind.Unspecified),
                Type: row.Cell(cols.Type).GetStringOrNull()?.Trim(),
                Discipline: row.Cell(cols.Discipline).GetStringOrNull()?.Trim(),
                Area: row.Cell(cols.Area).GetStringOrNull()?.Trim(),
                Venue: row.Cell(cols.Venue).GetStringOrNull()?.Trim(),
                FloorLevel: row.Cell(cols.FloorLevel).GetStringOrNull()?.Trim(),
                TransmittalIn: row.Cell(cols.TransmittalIn).GetStringOrNull()?.Trim()));
        }

        var read = parsed.Count;

        // Pass 2: drop the lines this project already holds, and the ones the file
        // repeats within itself. Counted, not silently swallowed: the whole point of
        // an append is that the operator can see a re-upload did nothing.
        var existingHashes = await _db.AconexRevisions
            .Where(a => a.ProjectId == projectId)
            .Select(a => a.LineHash)
            .ToListAsync(ct);
        var seen = existingHashes.ToHashSet(StringComparer.Ordinal);

        var fresh = new List<ParsedRevision>(parsed.Count);
        var duplicates = 0;
        foreach (var p in parsed)
        {
            if (!seen.Add(p.LineHash))
            {
                duplicates++;
                continue;
            }
            fresh.Add(p);
        }

        // Pass 3: insert the new lines. Flags are left false here — pass 4 computes
        // them over the whole surviving set, including the rows already in the table.
        var pending = new List<AconexRevision>(InsertChunkSize);
        var inserted = 0;

        async Task FlushAsync()
        {
            if (pending.Count == 0) return;
            _db.AconexRevisions.AddRange(pending);
            await _db.SaveChangesAsync(ct);
            foreach (var e in pending) _db.Entry(e).State = EntityState.Detached;
            pending.Clear();
        }

        foreach (var p in fresh)
        {
            pending.Add(new AconexRevision
            {
                ProjectId = projectId,
                ImportBatchId = importBatchId,
                LineHash = p.LineHash,
                FileType = p.FileType,
                FileName = p.FileName,
                AconexDocNo = p.AconexDocNo,
                DocNoFinal = p.DocNoFinal,
                Revision = p.Revision,
                Title = p.Title,
                AconexStatus = p.Status,
                ReviewStatus = p.ReviewStatus,
                DateModified = p.DateModified,
                Type = p.Type,
                Discipline = p.Discipline,
                Area = p.Area,
                Venue = p.Venue,
                FloorLevel = p.FloorLevel,
                TransmittalIn = p.TransmittalIn,
            });
            inserted++;

            if (pending.Count >= InsertChunkSize) await FlushAsync();
        }

        await FlushAsync();

        // Pass 4: recompute both flags over everything this project now holds.
        if (inserted > 0)
        {
            await RecomputeFlagsAsync(projectId, ct);
        }

        return new ImportResult(read, inserted, 0, skipped, warnings, duplicates);
    }

    // IsTerminated and IsLatest are aggregates over every row of a document, so they
    // are rebuilt from the full set rather than patched. Only rows whose flag actually
    // changes are written, which keeps a no-op re-upload from touching 25k rows.
    internal async Task RecomputeFlagsAsync(Guid projectId, CancellationToken ct)
    {
        // The batch's own timestamp orders the tie-break: an ImportBatchId is a random
        // Guid, so comparing ids would pick a winner at random, which is the very thing
        // the tie-break exists to stop.
        var batchOrder = await _db.ImportBatches
            .Where(b => b.ProjectId == projectId)
            .Select(b => new { b.Id, b.ImportedAt })
            .ToDictionaryAsync(b => b.Id, b => b.ImportedAt, ct);

        var rows = await _db.AconexRevisions
            .Where(a => a.ProjectId == projectId)
            .Select(a => new FlagRow(
                a.Id, a.ImportBatchId, a.DocNoFinal, a.Revision, a.DateModified,
                a.AconexStatus, a.ReviewStatus, a.IsTerminated, a.IsLatest))
            .ToListAsync(ct);

        var terminated = new HashSet<Guid>();
        var latest = new HashSet<Guid>();

        // Terminated: a row is terminated when its (document, revision) group holds a
        // terminal-status row at or after it — the workbook's COUNTIFS, which uses
        // `DateModified >= this`.
        foreach (var group in rows.GroupBy(r => (r.DocNoFinal, r.Revision)))
        {
            DateTime? latestTerminal = null;
            foreach (var row in group)
            {
                if (!IsTerminalStatus(row.AconexStatus, row.ReviewStatus)) continue;
                if (latestTerminal is null || row.DateModified > latestTerminal)
                {
                    latestTerminal = row.DateModified;
                }
            }
            if (latestTerminal is null) continue;

            foreach (var row in group)
            {
                if (row.DateModified <= latestTerminal) terminated.Add(row.Id);
            }
        }

        // Latest: the row with the greatest DateModified for its document. Ties are
        // possible now that a re-export can correct a title without moving the clock,
        // so exactly one wins — newest batch, then highest id — rather than all of
        // them, which would leave the tracker choosing arbitrarily.
        foreach (var group in rows.Where(r => !string.IsNullOrEmpty(r.DocNoFinal))
                                  .GroupBy(r => r.DocNoFinal!, StringComparer.OrdinalIgnoreCase))
        {
            var winner = group
                .OrderByDescending(r => r.DateModified)
                .ThenByDescending(r => batchOrder.TryGetValue(r.ImportBatchId, out var at)
                    ? at
                    : DateTime.MinValue)
                .ThenByDescending(r => r.Id)
                .First();
            latest.Add(winner.Id);
        }

        var changedTerminated = rows.Where(r => terminated.Contains(r.Id) != r.IsTerminated).ToList();
        var changedLatest = rows.Where(r => latest.Contains(r.Id) != r.IsLatest).ToList();

        foreach (var chunk in changedTerminated.Chunk(InsertChunkSize))
        {
            var on = chunk.Where(r => terminated.Contains(r.Id)).Select(r => r.Id).ToList();
            var off = chunk.Where(r => !terminated.Contains(r.Id)).Select(r => r.Id).ToList();
            if (on.Count > 0)
            {
                await _db.AconexRevisions.Where(a => on.Contains(a.Id))
                    .ExecuteUpdateAsync(u => u.SetProperty(a => a.IsTerminated, true), ct);
            }
            if (off.Count > 0)
            {
                await _db.AconexRevisions.Where(a => off.Contains(a.Id))
                    .ExecuteUpdateAsync(u => u.SetProperty(a => a.IsTerminated, false), ct);
            }
        }

        foreach (var chunk in changedLatest.Chunk(InsertChunkSize))
        {
            var on = chunk.Where(r => latest.Contains(r.Id)).Select(r => r.Id).ToList();
            var off = chunk.Where(r => !latest.Contains(r.Id)).Select(r => r.Id).ToList();
            if (on.Count > 0)
            {
                await _db.AconexRevisions.Where(a => on.Contains(a.Id))
                    .ExecuteUpdateAsync(u => u.SetProperty(a => a.IsLatest, true), ct);
            }
            if (off.Count > 0)
            {
                await _db.AconexRevisions.Where(a => off.Contains(a.Id))
                    .ExecuteUpdateAsync(u => u.SetProperty(a => a.IsLatest, false), ct);
            }
        }
    }

    private sealed record FlagRow(
        Guid Id, Guid ImportBatchId, string? DocNoFinal, string Revision, DateTime DateModified,
        string AconexStatus, string? ReviewStatus, bool IsTerminated, bool IsLatest);

    // Normalise the raw Aconex value onto a document number, or to null when it is
    // not one. Three rules, in order, and no substitution of any kind:
    //
    //   1. strip ALL whitespace
    //   2. collapse a run of hyphens to one — "…-2B30002--PDF"
    //   3. keep the first eight segments and drop the rest — which covers every
    //      export suffix the file carries ("-PDF", "-CAD", "-PDF-CAD", "-11",
    //      "-PDF.") without a list of them to maintain
    //
    // What is left must be eight non-empty segments; anything else has no number.
    // No segment is length-checked. The last one usually reads "0ZZ0004" (7 chars),
    // but 332 documents in the sample use 6 or 8 — e.g.
    // QF01012-NES-C04518-CAL-CIV-00-Z00000-000004 — and 174 of those carry real
    // Aconex history that a length rule would silently drop from every report.
    // Foreign-contract numbers (QF01012-BSB-C02310-...) are structurally valid and
    // normalise to themselves; they simply never match a MIDP document, which the
    // InMidp flag records. See docs/excel-analysis.md § 6.
    public static string? NormalizeDocNo(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var stripped = HyphenRunRegex.Replace(WhitespaceRegex.Replace(raw, string.Empty), "-");

        var segments = stripped.Split('-');
        if (segments.Length > SegmentCount)
        {
            segments = segments[..SegmentCount];
        }

        if (segments.Length != SegmentCount || segments.Any(string.IsNullOrEmpty))
        {
            return null;
        }

        return string.Join('-', segments);
    }

    private static bool IsTerminalStatus(string status, string? reviewStatus) =>
        (reviewStatus is not null && TerminalReviewStatuses.Contains(reviewStatus))
        || TerminalStatuses.Contains(status);

    private static int? FindHeaderRow(IExcelSheet sheet)
    {
        var last = Math.Min(sheet.RowCount, 30);
        for (var r = 1; r <= last; r++)
        {
            var row = sheet.Row(r);
            foreach (var cell in row.Cells())
            {
                if (string.Equals(cell.GetStringOrNull()?.Trim(), "Document No", StringComparison.OrdinalIgnoreCase))
                {
                    return r;
                }
            }
        }
        return null;
    }

    private static ColumnMap MapColumns(IExcelSheet sheet, int headerRow)
    {
        int Find(string label) => FindColumn(sheet, headerRow, label);
        return new ColumnMap(
            File: Find("File"),
            FileName: Find("File Name"),
            DocumentNo: Find("Document No"),
            Revision: Find("Revision"),
            Title: Find("Title"),
            Status: Find("Status"),
            ReviewStatus: Find("Review Status"),
            DateModified: Find("Date Modified"),
            Type: Find("Type"),
            Discipline: Find("Discipline"),
            Area: Find("Area - Geographical / Zone"),
            Venue: Find("Venue - Building / Facilities"),
            FloorLevel: Find("Floor Level"),
            TransmittalIn: Find("Transmittal In"));
    }

    private static int FindColumn(IExcelSheet sheet, int headerRow, string label)
    {
        var row = sheet.Row(headerRow);
        for (var c = 1; c <= sheet.ColumnCount; c++)
        {
            var value = row.Cell(c).GetStringOrNull()?.Trim();
            if (string.Equals(value, label, StringComparison.OrdinalIgnoreCase)) return c;
        }
        return -1;
    }

    private sealed record ColumnMap(
        int File, int FileName, int DocumentNo, int Revision, int Title,
        int Status, int ReviewStatus, int DateModified, int Type, int Discipline,
        int Area, int Venue, int FloorLevel, int TransmittalIn);

    private sealed record ParsedRevision(
        int RowNumber,
        string LineHash,
        string FileType, string FileName, string AconexDocNo, string? DocNoFinal,
        string Revision, string Title, string Status, string? ReviewStatus,
        DateTime DateModified,
        string? Type, string? Discipline, string? Area, string? Venue,
        string? FloorLevel, string? TransmittalIn);
}
