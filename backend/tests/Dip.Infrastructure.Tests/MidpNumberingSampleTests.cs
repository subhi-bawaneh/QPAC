using Dip.Application.Abstractions;
using Dip.Application.Documents;
using Dip.Infrastructure.Excel;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Seeding;
using FluentAssertions;
using Xunit;

namespace Dip.Infrastructure.Tests;

// The numbering rules, checked against samples/MIDP.xlsx through the real parser.
//
// The rule under test (docs/excel-analysis.md, finding "column A vs the fields"): the
// sheet's own DOCUMENT NUMBER is the value of record, normalised only by stripping
// whitespace, upper-casing and padding the zone to two digits. The number recomposed
// from the row's eight fields is a cross-check, and the rows where the two disagree
// are a finding — not something to silently resolve in either direction.
//
// Nothing here is hard-coded from this document: every expected count is derived from
// the sheet's own cells (hard rule 8).
public class MidpNumberingSampleTests
{
    private static readonly SerialWidths Widths = SerialWidths.Create(SeedPicklists.SerialWidths);

    private sealed record SheetRow(
        int RowNumber,
        string ColumnA,
        string Zone,
        string Sequence,
        string DrawingType,
        string Level,
        string DocType,
        DocumentRowParser.ParsedRow Parsed);

    private static IReadOnlyList<SheetRow> ReadRows()
    {
        using var workbook = new ClosedXmlReader().Open(SampleFiles.Open("MIDP.xlsx"));
        var sheet = workbook.Sheet("MIDP");
        var headerRow = DocumentRowParser.FindDocumentTableHeader(sheet);
        var columns = DocumentRowParser.MapDocumentColumns(sheet, headerRow);

        var rows = new List<SheetRow>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var parsed = DocumentRowParser.ParseRow(sheet, r, columns, Widths);
            if (parsed is null) continue;

            var row = sheet.Row(r);
            rows.Add(new SheetRow(
                RowNumber: r,
                ColumnA: row.Cell(columns.DocumentNumber).GetStringOrNull()?.Trim() ?? string.Empty,
                Zone: row.Cell(columns.F06).GetStringOrNull()?.Trim() ?? string.Empty,
                Sequence: row.Cell(columns.F08C).GetStringOrNull()?.Trim() ?? string.Empty,
                DrawingType: row.Cell(columns.F08A).GetStringOrNull()?.Trim() ?? string.Empty,
                Level: row.Cell(columns.F08B).GetStringOrNull()?.Trim() ?? string.Empty,
                DocType: row.Cell(columns.F04).GetStringOrNull()?.Trim() ?? string.Empty,
                Parsed: parsed));
        }

        rows.Should().NotBeEmpty();
        return rows;
    }

    // The number the register holds is always the number the sheet wrote.
    [Fact]
    public void EveryRow_TakesItsNumberFromColumnA()
    {
        var rows = ReadRows().Where(r => r.ColumnA.Length > 0).ToList();

        foreach (var row in rows)
        {
            row.Parsed.DocumentNumber.Should().Be(
                DocumentNumbering.NormalizeNumber(row.ColumnA),
                $"row {row.RowNumber} carries its own number");
        }
    }

    // The recompose agrees with column A everywhere except where the sheet disagrees
    // with itself — and that set is derived here from the sheet's own cells, not from
    // the parser's answer.
    [Fact]
    public void EveryRow_NormalisedColumnA_MatchesRecompose_ExceptTheDisagreementsTheSheetItselfContains()
    {
        var rows = ReadRows().Where(r => r.ColumnA.Length > 0).ToList();

        var reported = rows
            .Where(r => r.Parsed.Mismatch is not null)
            .Select(r => r.RowNumber)
            .ToHashSet();

        // Derived independently: the ZONE cell, or the serial the sheet typed, is not
        // what column A says.
        var derived = rows.Where(DisagreesWithItsOwnCells).Select(r => r.RowNumber).ToHashSet();

        reported.Should().BeEquivalentTo(derived);
        derived.Should().NotBeEmpty("the sample is known to contain rows whose fields contradict their number");

        // Every disagreement is in the zone or the serial — no other field drifts.
        rows.Where(r => r.Parsed.Mismatch is not null)
            .Select(r => r.Parsed.Mismatch!.Field)
            .Distinct()
            .Should().BeSubsetOf(new[] { "ZONE", "SEQUENCE" });
    }

    // Does the row's own ZONE / SEQUENCE cell contradict the number in column A?
    private static bool DisagreesWithItsOwnCells(SheetRow row)
    {
        var segments = Clean(row.ColumnA).Split('-');
        if (segments.Length != DocumentNumbering.SegmentCount) return false;

        if (!string.Equals(Pad(segments[5], 2), Pad(row.Zone, 2), StringComparison.Ordinal))
        {
            return true;
        }

        // The last segment is DRAWING TYPE + LEVEL + SEQUENCE with no separator, so the
        // serial is what remains after the row's own first two cells.
        var prefix = (row.DrawingType + row.Level).ToUpperInvariant();
        var last = segments[7];
        if (!last.StartsWith(prefix, StringComparison.Ordinal)) return true;

        var serialInNumber = last[prefix.Length..];
        var serialInCell = Pad(row.Sequence, Widths.For(row.DocType));
        return !string.Equals(serialInNumber, serialInCell, StringComparison.Ordinal);
    }

    // 19 rows of the sample write the zone as one digit; Aconex holds the padded form,
    // so normalisation is what makes them match. The count is read off the sheet.
    [Fact]
    public void ZonePadding_ReproducesAconexForm_OnTheUnpaddedRows()
    {
        var rows = ReadRows().Where(r => r.ColumnA.Length > 0).ToList();

        var unpadded = rows
            .Where(r => Clean(r.ColumnA).Split('-') is { Length: 8 } s && s[5].Length == 1)
            .ToList();

        unpadded.Should().NotBeEmpty("the sample writes some zones as a single digit");

        foreach (var row in unpadded)
        {
            var written = Clean(row.ColumnA).Split('-');
            var stored = row.Parsed.DocumentNumber.Split('-');

            stored[5].Should().Be("0" + written[5]);
            // Only the zone moves.
            stored.Where((_, i) => i != 5).Should().Equal(written.Where((_, i) => i != 5));
        }
    }

    // The seeded widths are the sheet's own: for each type, the width most of its rows
    // actually use.
    [Fact]
    public void Widths_ByType_MatchTheSheetsOwnNumbers()
    {
        var rows = ReadRows().Where(r => r.ColumnA.Length > 0).ToList();

        foreach (var (docType, seededWidth) in SeedPicklists.SerialWidths)
        {
            var lengths = rows
                .Where(r => string.Equals(r.DocType, docType, StringComparison.OrdinalIgnoreCase))
                .Select(SerialLength)
                .Where(length => length > 0)
                .ToList();

            if (lengths.Count == 0) continue;   // a seeded type the sample does not use

            var modal = lengths.GroupBy(l => l).OrderByDescending(g => g.Count()).First().Key;
            modal.Should().Be(seededWidth, $"{docType} numbers in the sheet are {modal} digits");
        }
    }

    private static int SerialLength(SheetRow row)
    {
        var segments = Clean(row.ColumnA).Split('-');
        if (segments.Length != DocumentNumbering.SegmentCount) return 0;

        var prefix = (row.DrawingType + row.Level).ToUpperInvariant();
        return segments[7].StartsWith(prefix, StringComparison.Ordinal)
            ? segments[7].Length - prefix.Length
            : 0;
    }

    private static string Clean(string raw) =>
        new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();

    private static string Pad(string value, int width) => value.Trim().PadLeft(width, '0');
}
