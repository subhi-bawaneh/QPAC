using System.Text.RegularExpressions;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Reads the Aconex History export (~25,246 rows) and materialises one
// AconexRevision per row with:
//   - DocNoFinal: whitespace-stripped, `-PDF` suffix removed. If the shape
//     doesn't match the 8-segment MIDP pattern (last segment 7 chars),
//     the value becomes "XXX" so downstream filters can exclude it.
//   - IsTerminated: a same-DocNo + same-Revision row at or after this row
//     has ReviewStatus="Terminated" OR Status in ("Closed", "No Longer In Use").
//   - IsLatest: this row has the max DateModified for its DocNoFinal.
//   - InMidp: DocNoFinal is present in the Documents table, or IsTerminated.
//
// Always writes to Live (Aconex history is versioned via ImportBatch; there
// is no Draft/Live workflow for it).
public sealed class AconexHistoryImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    private const int InsertChunkSize = 2000;
    public const string InvalidDocNoSentinel = "XXX";

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);
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

    public async Task<ImportResult> ImportAsync(
        Guid projectId,
        string filePath,
        Guid importBatchId,
        string importedBy,
        CancellationToken ct)
    {
        _ = importedBy;

        using var wb = _reader.Open(filePath);
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

        // Wipe previous Aconex data for this project so IsLatest / IsTerminated
        // reflect only the current file. History is idempotent by design.
        await _db.AconexRevisions
            .Where(a => a.ProjectId == projectId)
            .ExecuteDeleteAsync(ct);

        // Preload MIDP document numbers for InMidp lookup.
        var midpNumbers = await _db.Documents
            .Where(d => d.ProjectId == projectId)
            .Select(d => d.DocumentNumber)
            .ToListAsync(ct);
        var midpSet = midpNumbers.ToHashSet(StringComparer.OrdinalIgnoreCase);

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

            parsed.Add(new ParsedRevision(
                RowNumber: r,
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

        // Pass 2: compute IsTerminated grouped by (DocNoFinal, Revision).
        // A row is Terminated if any row in its (DocNo, Rev) group with
        // DateModified >= this row's DateModified has terminal status.
        var terminatedFlags = new bool[parsed.Count];
        var byDocRev = parsed
            .Select((p, i) => (Row: p, Index: i))
            .GroupBy(x => (x.Row.DocNoFinal, x.Row.Revision));
        foreach (var group in byDocRev)
        {
            // Track earliest DateModified among terminal-status rows.
            DateTime? earliestTerminal = null;
            foreach (var (p, _) in group)
            {
                if (IsTerminalStatus(p.Status, p.ReviewStatus))
                {
                    if (earliestTerminal is null || p.DateModified < earliestTerminal)
                    {
                        earliestTerminal = p.DateModified;
                    }
                }
            }
            if (earliestTerminal is null) continue;

            // Any row with DateModified <= max terminal date is Terminated,
            // but since the formula says "DateModified >= this AND terminal",
            // equivalently: a row is Terminated when the group has ANY terminal
            // row at or after it. Iterating with the earliest-terminal covers
            // all rows at or before it.
            //
            // Actually the original COUNTIFS uses `DateModified >= this`, which
            // means R is Terminated if a terminal row exists whose DateModified
            // is >= R.DateModified. So we mark R Terminated when the LATEST
            // terminal row's DateModified >= R.DateModified.
            DateTime latestTerminal = group
                .Where(x => IsTerminalStatus(x.Row.Status, x.Row.ReviewStatus))
                .Max(x => x.Row.DateModified);
            foreach (var (p, i) in group)
            {
                if (p.DateModified <= latestTerminal)
                {
                    terminatedFlags[i] = true;
                }
            }
        }

        // Pass 3: compute IsLatest grouped by DocNoFinal (ignore XXX entries).
        var latestFlags = new bool[parsed.Count];
        var byDocFinal = parsed
            .Select((p, i) => (Row: p, Index: i))
            .Where(x => !string.Equals(x.Row.DocNoFinal, InvalidDocNoSentinel, StringComparison.Ordinal))
            .GroupBy(x => x.Row.DocNoFinal);
        foreach (var group in byDocFinal)
        {
            var max = group.Max(x => x.Row.DateModified);
            // Ties: mark ALL rows sharing the max as Latest (rare — sub-second precision).
            foreach (var (p, i) in group)
            {
                if (p.DateModified == max) latestFlags[i] = true;
            }
        }

        // Pass 4: chunk-insert AconexRevision rows with computed flags.
        var pending = new List<AconexRevision>(InsertChunkSize);
        var inserted = 0;
        for (var i = 0; i < parsed.Count; i++)
        {
            var p = parsed[i];
            var isTerminated = terminatedFlags[i];
            var isLatest = latestFlags[i];
            var inMidp = isTerminated || midpSet.Contains(p.DocNoFinal);

            pending.Add(new AconexRevision
            {
                ProjectId = projectId,
                ImportBatchId = importBatchId,
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
                IsTerminated = isTerminated,
                IsLatest = isLatest,
                InMidp = inMidp,
            });
            inserted++;

            if (pending.Count >= InsertChunkSize)
            {
                _db.AconexRevisions.AddRange(pending);
                await _db.SaveChangesAsync(ct);
                foreach (var e in pending) _db.Entry(e).State = EntityState.Detached;
                pending.Clear();
            }
        }

        if (pending.Count > 0)
        {
            _db.AconexRevisions.AddRange(pending);
            await _db.SaveChangesAsync(ct);
        }

        return new ImportResult(parsed.Count, inserted, 0, skipped, warnings);
    }

    // Normalise: strip ALL whitespace, remove trailing `-PDF` (case-insensitive),
    // then check the document-number shape: 8 non-empty dash-separated segments.
    // Anything else is "XXX" so filters can exclude it.
    //
    // The last segment is NOT length-checked. It usually reads "0ZZ0004" (7 chars),
    // but 332 documents in the sample use 6 or 8 — e.g.
    // QF01012-NES-C04518-CAL-CIV-00-Z00000-000004 — and 174 of those carry real
    // Aconex history that a length rule would silently drop from every report.
    // Foreign-contract numbers (QF01012-BSB-C02310-...) are structurally valid and
    // normalise to themselves; they simply never match a MIDP document, which the
    // InMidp flag records. See docs/excel-analysis.md § 6.
    public static string NormalizeDocNo(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return InvalidDocNoSentinel;

        var stripped = WhitespaceRegex.Replace(raw, string.Empty);
        if (stripped.EndsWith("-PDF", StringComparison.OrdinalIgnoreCase))
        {
            stripped = stripped[..^4];
        }

        var segments = stripped.Split('-');
        if (segments.Length != 8 || segments.Any(string.IsNullOrEmpty))
        {
            return InvalidDocNoSentinel;
        }

        return stripped;
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
        string FileType, string FileName, string AconexDocNo, string DocNoFinal,
        string Revision, string Title, string Status, string? ReviewStatus,
        DateTime DateModified,
        string? Type, string? Discipline, string? Area, string? Venue,
        string? FloorLevel, string? TransmittalIn);
}
