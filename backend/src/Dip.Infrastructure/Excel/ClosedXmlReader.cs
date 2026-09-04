using System.Globalization;
using System.IO;
using ClosedXML.Excel;
using Dip.Application.Abstractions;

namespace Dip.Infrastructure.Excel;

// Concrete IExcelReader over ClosedXML. Wraps XLWorkbook and its cells so
// importers can be written against the port and unit-tested without touching
// a real .xlsx file.
public sealed class ClosedXmlReader : IExcelReader
{
    public IExcelWorkbook Open(Stream stream) => new WorkbookWrapper(new XLWorkbook(stream));
    public IExcelWorkbook Open(string filePath) => new WorkbookWrapper(new XLWorkbook(filePath));

    private sealed class WorkbookWrapper : IExcelWorkbook
    {
        private readonly XLWorkbook _wb;

        public WorkbookWrapper(XLWorkbook wb) => _wb = wb;

        public IReadOnlyList<string> SheetNames => _wb.Worksheets.Select(ws => ws.Name).ToList();

        public IExcelSheet Sheet(string name) => new SheetWrapper(_wb.Worksheet(name));

        public bool TryGetSheet(string name, out IExcelSheet? sheet)
        {
            if (_wb.Worksheets.TryGetWorksheet(name, out var ws))
            {
                sheet = new SheetWrapper(ws);
                return true;
            }
            sheet = null;
            return false;
        }

        public void Dispose() => _wb.Dispose();
    }

    private sealed class SheetWrapper : IExcelSheet
    {
        private readonly IXLWorksheet _sheet;

        public SheetWrapper(IXLWorksheet sheet) => _sheet = sheet;

        public string Name => _sheet.Name;

        public int RowCount => _sheet.LastRowUsed()?.RowNumber() ?? 0;
        public int ColumnCount => _sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        public IExcelRow Row(int rowNumber) => new RowWrapper(_sheet.Row(rowNumber));

        public IEnumerable<IExcelRow> Rows(int fromRow, int toRow)
        {
            for (var r = fromRow; r <= toRow; r++)
            {
                yield return new RowWrapper(_sheet.Row(r));
            }
        }

        public int? FindRowContaining(string text, int inColumn)
        {
            var last = _sheet.LastRowUsed()?.RowNumber() ?? 0;
            for (var r = 1; r <= last; r++)
            {
                var cell = _sheet.Cell(r, inColumn);
                if (Equals(cell.GetFormattedString(), text) || Contains(cell.GetFormattedString(), text))
                {
                    return r;
                }
            }
            return null;
        }

        public int? FindRowContainingAnywhere(string text)
        {
            var last = _sheet.LastRowUsed()?.RowNumber() ?? 0;
            var lastCol = _sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
            for (var r = 1; r <= last; r++)
            {
                for (var c = 1; c <= lastCol; c++)
                {
                    if (Contains(_sheet.Cell(r, c).GetFormattedString(), text))
                    {
                        return r;
                    }
                }
            }
            return null;
        }

        private static bool Contains(string? haystack, string needle) =>
            !string.IsNullOrEmpty(haystack) && haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class RowWrapper : IExcelRow
    {
        private readonly IXLRow _row;

        public RowWrapper(IXLRow row) => _row = row;

        public int RowNumber => _row.RowNumber();

        public IExcelCell Cell(int columnNumber) => new CellWrapper(_row.Cell(columnNumber));

        public IEnumerable<IExcelCell> Cells()
        {
            foreach (var c in _row.CellsUsed())
            {
                yield return new CellWrapper(c);
            }
        }
    }

    private sealed class CellWrapper : IExcelCell
    {
        private readonly IXLCell _cell;

        public CellWrapper(IXLCell cell) => _cell = cell;

        public int RowNumber => _cell.Address.RowNumber;
        public int ColumnNumber => _cell.Address.ColumnNumber;
        public string Address => _cell.Address.ToString() ?? string.Empty;
        public bool IsEmpty => _cell.IsEmpty();

        public string GetString() => _cell.GetFormattedString();
        public string? GetStringOrNull() => _cell.IsEmpty() ? null : _cell.GetFormattedString();

        public DateTime? GetDateTime()
        {
            if (_cell.IsEmpty()) return null;
            if (_cell.TryGetValue<DateTime>(out var dt)) return dt;
            // Some Aconex exports store dates as text; fall back to Invariant.
            if (_cell.TryGetValue<string>(out var s)
                && DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
            {
                return parsed;
            }
            return null;
        }

        public decimal? GetDecimal()
        {
            if (_cell.IsEmpty()) return null;
            if (_cell.TryGetValue<decimal>(out var d)) return d;
            if (_cell.TryGetValue<double>(out var db)) return (decimal)db;
            if (_cell.TryGetValue<string>(out var s)
                && decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
            {
                return parsed;
            }
            return null;
        }

        public int? GetInt() => (int?)GetDecimal();
    }
}
