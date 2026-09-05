using ClosedXML.Excel;
using Dip.Application.Abstractions;

namespace Dip.Infrastructure.Excel;

// Writes report exports as xlsx. Kept deliberately plain: a bold header row, frozen
// so it stays visible, an auto-filter, and auto-fitted columns.
public sealed class ClosedXmlReportExporter : IReportExporter
{
    // ClosedXML's sheet names cannot exceed 31 characters or contain these.
    private static readonly char[] InvalidSheetNameChars = ['\\', '/', '*', '?', ':', '[', ']'];

    public byte[] ToWorkbook(IReadOnlyList<ExportSheet> sheets)
    {
        using var workbook = new XLWorkbook();

        foreach (var sheet in sheets)
        {
            var worksheet = workbook.Worksheets.Add(SheetName(sheet.Name, workbook));

            for (var c = 0; c < sheet.Headers.Count; c++)
            {
                worksheet.Cell(1, c + 1).Value = sheet.Headers[c];
            }
            worksheet.Row(1).Style.Font.Bold = true;
            worksheet.SheetView.FreezeRows(1);

            for (var r = 0; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                for (var c = 0; c < row.Count; c++)
                {
                    Write(worksheet.Cell(r + 2, c + 1), row[c]);
                }
            }

            if (sheet.Headers.Count > 0)
            {
                worksheet.Range(1, 1, Math.Max(sheet.Rows.Count + 1, 1), sheet.Headers.Count)
                    .SetAutoFilter();
                worksheet.Columns().AdjustToContents(1, 200);
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void Write(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null:
                break;
            case string text:
                // Leading zeros are significant in document numbers and revisions, so
                // the cell is written as text rather than letting Excel coerce it.
                cell.SetValue(text);
                cell.Style.NumberFormat.Format = "@";
                break;
            case DateTime date:
                cell.Value = date;
                cell.Style.DateFormat.Format = date.TimeOfDay == TimeSpan.Zero
                    ? "dd-mmm-yyyy"
                    : "dd-mmm-yyyy hh:mm:ss";
                break;
            case bool flag:
                cell.Value = flag;
                break;
            case decimal number:
                cell.Value = number;
                break;
            case double number:
                cell.Value = number;
                break;
            case int number:
                cell.Value = number;
                break;
            case long number:
                cell.Value = number;
                break;
            case Enum enumeration:
                cell.SetValue(enumeration.ToString());
                break;
            default:
                cell.SetValue(value.ToString());
                break;
        }
    }

    private static string SheetName(string requested, XLWorkbook workbook)
    {
        var name = requested;
        foreach (var invalid in InvalidSheetNameChars)
        {
            name = name.Replace(invalid, '-');
        }
        if (name.Length > 31) name = name[..31];
        if (string.IsNullOrWhiteSpace(name)) name = "Sheet";

        // Two reports could otherwise collide after truncation.
        var candidate = name;
        var suffix = 2;
        while (workbook.Worksheets.Contains(candidate))
        {
            var trimmed = name.Length > 28 ? name[..28] : name;
            candidate = $"{trimmed}-{suffix++}";
        }
        return candidate;
    }
}
