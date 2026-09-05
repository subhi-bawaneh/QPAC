namespace Dip.Application.Abstractions;

/// A sheet of a workbook: a header row and the rows beneath it.
/// Values are written with their own type, so dates stay dates and numbers stay
/// numbers in the exported file rather than becoming text.
public sealed record ExportSheet(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<object?>> Rows);

public interface IReportExporter
{
    /// Renders one or more sheets to an xlsx workbook.
    byte[] ToWorkbook(IReadOnlyList<ExportSheet> sheets);
}
