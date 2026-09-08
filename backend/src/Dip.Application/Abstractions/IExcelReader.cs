using System.IO;

namespace Dip.Application.Abstractions;

// Abstraction over ClosedXML so importers stay testable and the engine layer
// never has to reference the ClosedXML package. Concrete reader lives in
// Dip.Infrastructure/Excel.
public interface IExcelReader
{
    // Stream only: workbook bytes come from FileBlob, never from the file system
    // (refactor-plan decision D2).
    IExcelWorkbook Open(Stream stream);
}

public interface IExcelWorkbook : IDisposable
{
    IReadOnlyList<string> SheetNames { get; }
    IExcelSheet Sheet(string name);
    bool TryGetSheet(string name, out IExcelSheet? sheet);
}

public interface IExcelSheet
{
    string Name { get; }
    int RowCount { get; }
    int ColumnCount { get; }
    IExcelRow Row(int rowNumber); // 1-based
    IEnumerable<IExcelRow> Rows(int fromRow, int toRow);
    int? FindRowContaining(string text, int inColumn); // 1-based column
    int? FindRowContainingAnywhere(string text);
}

public interface IExcelRow
{
    int RowNumber { get; }
    IExcelCell Cell(int columnNumber); // 1-based
    IEnumerable<IExcelCell> Cells();
}

public interface IExcelCell
{
    int RowNumber { get; }
    int ColumnNumber { get; }
    string Address { get; }
    string GetString();
    string? GetStringOrNull();
    DateTime? GetDateTime();
    decimal? GetDecimal();
    int? GetInt();
    bool IsEmpty { get; }
}
