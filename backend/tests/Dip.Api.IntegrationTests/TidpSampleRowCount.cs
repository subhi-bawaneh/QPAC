using ClosedXML.Excel;

namespace Dip.Api.IntegrationTests;

// Counts the populated document rows of samples/TIDP-STL.xlsx by reading the
// workbook, so the flow tests assert against the sample rather than a literal.
internal static class TidpSampleRowCount
{
    public static int Value { get; } = Count();

    private static int Count()
    {
        using var workbook = new XLWorkbook(TestHelpers.SamplePath("TIDP-STL.xlsx"));
        var sheet = workbook.Worksheet("TIDP_Sheet");
        var last = sheet.LastRowUsed()?.RowNumber() ?? 0;

        var headerRow = 0;
        for (var r = 1; r <= last; r++)
        {
            if (string.Equals(sheet.Cell(r, 1).GetString().Trim(), "DOCUMENT NUMBER",
                    StringComparison.OrdinalIgnoreCase))
            {
                headerRow = r;
                break;
            }
        }

        if (headerRow == 0) throw new InvalidOperationException("No DOCUMENT NUMBER header in TIDP_Sheet");

        // A row counts when PROJECT (L), DOCUMENT TYPE (O) and DISCIPLINE (P) are
        // populated — exactly the test DocumentRowParser applies before it skips a
        // template row.
        var rows = 0;
        for (var r = headerRow + 1; r <= last; r++)
        {
            var project = sheet.Cell(r, 12).GetString().Trim();
            var docType = sheet.Cell(r, 15).GetString().Trim();
            var discipline = sheet.Cell(r, 16).GetString().Trim();
            if (project.Length > 0 && docType.Length > 0 && discipline.Length > 0) rows++;
        }
        return rows;
    }
}
