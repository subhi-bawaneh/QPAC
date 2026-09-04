using Dip.Application.Abstractions;

namespace Dip.Infrastructure.Importers;

// Shared column-mapping + row-parsing helpers used by TidpImporter and
// MidpImporter (both consume the same A..AH schema described in
// docs/excel-analysis.md § 2/3).
internal static class DocumentRowParser
{
    public sealed record DocumentColumnMap(
        int Title, int ExtractedFromModel, int ScopeArea, int AuthoringSoftware,
        int ExchangeFormat, int Scale, int DeliveryMilestone, int PackageName,
        int ActivityId, int ClassificationCode,
        int F01, int F02, int F03, int F04, int F05, int F06, int F07,
        int F08A, int F08B, int F08C, int CorporateDiscipline,
        int Ex1Author, int Ex1Geometrical, int Ex1NonGeometrical,
        int Ex1Duration, int Ex1Predecessor, int Ex1ExchangeDate,
        int Ex2Author, int Ex2Geometrical, int Ex2NonGeometrical,
        int Ex2Duration, int Ex2Predecessor, int Ex2ExchangeDate);

    public sealed record ParsedRow(
        string DocumentNumber, string Title,
        string? ExtractedFromModel, string? ScopeArea, string? AuthoringSoftware,
        string? ExchangeFormat, string? Scale, DateTime? DeliveryMilestone,
        string? PackageName, string? ActivityId, string? ClassificationCode,
        string F01, string F02, string F03, string F04, string F05, string F06, string F07,
        string F08A, string F08B, string F08C, string CorporateDiscipline,
        ExchangeRow? Exchange1, ExchangeRow? Exchange2);

    public sealed record ExchangeRow(
        string? Author, string? Geometrical, string? NonGeometrical,
        decimal? DurationDays, string? Predecessor, DateTime? ExchangeDate);

    public static int FindDocumentTableHeader(IExcelSheet sheet)
    {
        var last = Math.Min(sheet.RowCount, 30);
        for (var r = 1; r <= last; r++)
        {
            var value = sheet.Row(r).Cell(1).GetStringOrNull()?.Trim();
            if (string.Equals(value, "DOCUMENT NUMBER", StringComparison.OrdinalIgnoreCase))
            {
                return r;
            }
        }
        throw new InvalidOperationException("Cannot find 'DOCUMENT NUMBER' header row");
    }

    public static DocumentColumnMap MapDocumentColumns(IExcelSheet sheet, int headerRow)
    {
        int Find(string label) => FindColumn(sheet, headerRow, label);
        return new DocumentColumnMap(
            Title: Find("DOCUMENT TITLE"),
            ExtractedFromModel: Find("EXTRACTED FROM MODEL"),
            ScopeArea: Find("SCOPE AREA"),
            AuthoringSoftware: Find("AUTHORING SOFTWARE"),
            ExchangeFormat: Find("EXCHANGE FORMAT"),
            Scale: Find("SCALE"),
            DeliveryMilestone: Find("DELIVERY MILESTONE"),
            PackageName: Find("PACKAGE NAME"),
            ActivityId: Find("ACTIVITY ID"),
            ClassificationCode: Find("CLASSIFICATION CODE"),
            F01: Find("PROJECT"),
            F02: Find("ORIGINATOR"),
            F03: Find("CONTRACT"),
            F04: Find("DOCUMENT TYPE"),
            F05: Find("DISCIPLINE"),
            F06: Find("AREA/ZONE"),
            F07: Find("VENUE/BUILDING"),
            F08A: Find("DRAWING TYPE"),
            F08B: Find("LEVEL"),
            F08C: Find("SEQUENCE NUMBER"),
            CorporateDiscipline: Find("CORPORATE DISCIPLINE"),
            Ex1Author: Find("01-AUTHOR"),
            Ex1Geometrical: Find("01-GEOMETRICAL"),
            Ex1NonGeometrical: Find("01-NON GEOMETRICAL"),
            Ex1Duration: Find("01-DURATION (DAYS)"),
            Ex1Predecessor: Find("01-PREDECESSOR"),
            Ex1ExchangeDate: Find("01-EXCHANGE DATE"),
            Ex2Author: Find("02-AUTHOR"),
            Ex2Geometrical: Find("02-GEOMETRICAL"),
            Ex2NonGeometrical: Find("02-NON GEOMETRICAL"),
            Ex2Duration: Find("02-DURATION (DAYS)"),
            Ex2Predecessor: Find("02-PREDECESSOR"),
            Ex2ExchangeDate: Find("02-EXCHANGE DATE"));
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

    public static ParsedRow? ParseRow(IExcelSheet sheet, int r, DocumentColumnMap col)
    {
        var row = sheet.Row(r);

        var f01 = ReadRequired(row, col.F01);
        var f02 = ReadRequired(row, col.F02);
        var f03 = ReadRequired(row, col.F03);
        var f04 = ReadRequired(row, col.F04);
        var f05 = ReadRequired(row, col.F05);
        var f06 = ReadRequired(row, col.F06).PadLeft(2, '0');
        var f07 = ReadRequired(row, col.F07);
        var f08a = ReadRequired(row, col.F08A);
        var f08b = ReadRequired(row, col.F08B);
        var f08c = ReadRequired(row, col.F08C).PadLeft(4, '0');

        // Required fields blank -> skip (template rows below the last populated row).
        if (string.IsNullOrEmpty(f01) || string.IsNullOrEmpty(f04) || string.IsNullOrEmpty(f05))
        {
            return null;
        }

        var documentNumber = $"{f01}-{f02}-{f03}-{f04}-{f05}-{f06}-{f07}-{f08a}{f08b}{f08c}";

        return new ParsedRow(
            DocumentNumber: documentNumber,
            Title: ReadRequired(row, col.Title),
            ExtractedFromModel: ReadOptional(row, col.ExtractedFromModel),
            ScopeArea: ReadOptional(row, col.ScopeArea),
            AuthoringSoftware: ReadOptional(row, col.AuthoringSoftware),
            ExchangeFormat: ReadOptional(row, col.ExchangeFormat),
            Scale: ReadOptional(row, col.Scale),
            DeliveryMilestone: ReadDate(row, col.DeliveryMilestone),
            PackageName: ReadOptional(row, col.PackageName),
            ActivityId: ReadOptional(row, col.ActivityId),
            ClassificationCode: ReadOptional(row, col.ClassificationCode),
            F01: f01, F02: f02, F03: f03, F04: f04, F05: f05, F06: f06,
            F07: f07, F08A: f08a, F08B: f08b, F08C: f08c,
            CorporateDiscipline: ReadRequired(row, col.CorporateDiscipline),
            Exchange1: ReadExchange(row, col.Ex1Author, col.Ex1Geometrical, col.Ex1NonGeometrical,
                col.Ex1Duration, col.Ex1Predecessor, col.Ex1ExchangeDate),
            Exchange2: ReadExchange(row, col.Ex2Author, col.Ex2Geometrical, col.Ex2NonGeometrical,
                col.Ex2Duration, col.Ex2Predecessor, col.Ex2ExchangeDate));
    }

    private static ExchangeRow? ReadExchange(
        IExcelRow row, int author, int geometrical, int nonGeometrical,
        int duration, int predecessor, int exchangeDate)
    {
        var authorValue = ReadOptional(row, author);
        var geoValue = ReadOptional(row, geometrical);
        var nonGeoValue = ReadOptional(row, nonGeometrical);
        var durationValue = duration > 0 ? row.Cell(duration).GetDecimal() : null;
        var predValue = ReadOptional(row, predecessor);
        var dateValue = ReadDate(row, exchangeDate);
        if (authorValue is null && geoValue is null && nonGeoValue is null
            && durationValue is null && predValue is null && dateValue is null)
        {
            return null;
        }
        return new ExchangeRow(authorValue, geoValue, nonGeoValue, durationValue, predValue, dateValue);
    }

    public static string ReadRequired(IExcelRow row, int column) =>
        column > 0 ? (row.Cell(column).GetStringOrNull()?.Trim() ?? string.Empty) : string.Empty;

    public static string? ReadOptional(IExcelRow row, int column) =>
        column > 0 ? row.Cell(column).GetStringOrNull()?.Trim() : null;

    public static DateTime? ReadDate(IExcelRow row, int column) =>
        column > 0 ? row.Cell(column).GetDateTime() : null;
}
