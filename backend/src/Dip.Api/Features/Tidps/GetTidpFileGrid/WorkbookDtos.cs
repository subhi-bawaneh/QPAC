namespace Dip.Api.Features.Tidps.GetTidpFileGrid;

// The spreadsheet the viewer renders. Every cell is a string so leading zeros
// (Zone "00", Sequence "0004") survive the round trip exactly as Excel shows them.
public sealed record WorkbookDto(
    Guid FileId,
    string FileName,
    string Discipline,
    DateTime UploadedAt,
    IReadOnlyList<string> Sheets,
    WorkbookSheetDto Sheet);

public sealed record WorkbookSheetDto(
    string Name,
    IReadOnlyList<WorkbookHeaderCell> HeaderBlock,
    IReadOnlyList<WorkbookColumn> Columns,
    int TotalRows,
    int Page,
    int PageSize,
    IReadOnlyList<WorkbookRow> Rows);

public sealed record WorkbookHeaderCell(string Label, string? Value);

public sealed record WorkbookColumn(
    string Letter,
    string Title,
    string Key,
    int Width,
    bool Editable,
    string Kind);

public sealed record WorkbookRow(
    Guid RowId,
    int RowNumber,
    IReadOnlyList<string?> Cells,
    string? State);
