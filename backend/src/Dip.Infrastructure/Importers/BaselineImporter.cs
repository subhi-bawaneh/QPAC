using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Reads the `Baseline` sheet (columns: [1]..[7] WBS, Package, Activity Code,
// Activity=Submittal|Approval, Original Duration, Start, Finish, Used).
// Upserts by (ProjectId, ActivityCode).
//
// The `Baseline` sheet appears in multiple workbooks with slightly different
// header rows (row 2 in Baseline.xlsx / TIDP-STL.xlsx, row 5 in Tracker.xlsx).
// We locate it by searching for the "Activity Code" text.
public sealed class BaselineImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    public BaselineImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<ImportResult> ImportAsync(Guid projectId, Stream content, bool replace, CancellationToken ct)
    {
        using var wb = _reader.Open(content);
        // Different workbooks use different names for the same sheet:
        //   Baseline.xlsx   -> "ENG_BL"  (P6 export)
        //   TIDP-STL.xlsx   -> "Baseline"
        //   Tracker.xlsx    -> "Baseline"
        IExcelSheet? sheet = null;
        foreach (var candidate in new[] { "Baseline", "ENG_BL" })
        {
            if (wb.TryGetSheet(candidate, out sheet) && sheet is not null) break;
        }
        if (sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'Baseline' or 'ENG_BL' sheet in workbook" });
        }

        var headerRow = FindHeaderRow(sheet)
            ?? throw new InvalidOperationException("Cannot find 'Activity Code' header in Baseline sheet");

        var columns = MapColumns(sheet, headerRow);
        var lastRow = sheet.RowCount;

        // If replace: wipe first so stale rows can't linger. Otherwise upsert.
        if (replace)
        {
            await _db.BaselineActivities
                .Where(b => b.ProjectId == projectId)
                .ExecuteDeleteAsync(ct);
        }

        var existingRows = replace
            ? new List<BaselineActivity>()
            : await _db.BaselineActivities.Where(b => b.ProjectId == projectId).ToListAsync(ct);
        var existing = existingRows.ToDictionary(b => b.ActivityCode, StringComparer.OrdinalIgnoreCase);

        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var row = sheet.Row(r);
            var activityCode = row.Cell(columns.ActivityCode).GetStringOrNull()?.Trim();
            var activityType = row.Cell(columns.Activity).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(activityCode) || string.IsNullOrEmpty(activityType))
            {
                continue;
            }

            read++;

            var typeEnum = activityType.Equals("Submittal", StringComparison.OrdinalIgnoreCase)
                ? BaselineActivityType.Submittal
                : activityType.Equals("Approval", StringComparison.OrdinalIgnoreCase)
                    ? BaselineActivityType.Approval
                    : (BaselineActivityType?)null;

            if (typeEnum is null)
            {
                warnings.Add($"Row {r}: unknown activity type '{activityType}' - skipped");
                skipped++;
                continue;
            }

            var package = row.Cell(columns.Package).GetStringOrNull()?.Trim() ?? string.Empty;
            var duration = row.Cell(columns.OriginalDuration).GetInt() ?? 0;
            var start = row.Cell(columns.Start).GetDateTime()
                ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Unspecified);
            var finish = row.Cell(columns.Finish).GetDateTime()
                ?? DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Unspecified);

            string ReadWbs(int col) => col > 0 ? row.Cell(col).GetStringOrNull()?.Trim() ?? string.Empty : string.Empty;

            if (existing.TryGetValue(activityCode, out var current))
            {
                var changed =
                    current.Package != package ||
                    current.Type != typeEnum ||
                    current.OriginalDuration != duration ||
                    current.Start != start ||
                    current.Finish != finish;

                current.Package = package;
                current.Type = typeEnum.Value;
                current.OriginalDuration = duration;
                current.Start = start;
                current.Finish = finish;
                current.WbsLevel1 = ReadWbs(columns.Wbs1);
                current.WbsLevel2 = ReadWbs(columns.Wbs2);
                current.WbsLevel3 = ReadWbs(columns.Wbs3);
                current.WbsLevel4 = ReadWbs(columns.Wbs4);
                current.WbsLevel5 = ReadWbs(columns.Wbs5);
                current.WbsLevel6 = ReadWbs(columns.Wbs6);
                current.WbsLevel7 = ReadWbs(columns.Wbs7);
                if (changed) updated++;
            }
            else
            {
                _db.BaselineActivities.Add(new BaselineActivity
                {
                    ProjectId = projectId,
                    ActivityCode = activityCode,
                    Package = package,
                    Type = typeEnum.Value,
                    OriginalDuration = duration,
                    Start = start,
                    Finish = finish,
                    WbsLevel1 = ReadWbs(columns.Wbs1),
                    WbsLevel2 = ReadWbs(columns.Wbs2),
                    WbsLevel3 = ReadWbs(columns.Wbs3),
                    WbsLevel4 = ReadWbs(columns.Wbs4),
                    WbsLevel5 = ReadWbs(columns.Wbs5),
                    WbsLevel6 = ReadWbs(columns.Wbs6),
                    WbsLevel7 = ReadWbs(columns.Wbs7),
                });
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }

    // Look through the first 30 rows for "Activity Code" in any cell.
    private static int? FindHeaderRow(IExcelSheet sheet)
    {
        var last = Math.Min(sheet.RowCount, 30);
        for (var r = 1; r <= last; r++)
        {
            var row = sheet.Row(r);
            foreach (var cell in row.Cells())
            {
                if (string.Equals(cell.GetStringOrNull()?.Trim(), "Activity Code", StringComparison.OrdinalIgnoreCase))
                {
                    return r;
                }
            }
        }
        return null;
    }

    private sealed record ColumnMap(
        int Wbs1, int Wbs2, int Wbs3, int Wbs4, int Wbs5, int Wbs6, int Wbs7,
        int Package, int ActivityCode, int Activity, int OriginalDuration, int Start, int Finish);

    private static ColumnMap MapColumns(IExcelSheet sheet, int headerRow)
    {
        int Find(string label) => FindColumn(sheet, headerRow, label);
        return new ColumnMap(
            Wbs1: Find("1"), Wbs2: Find("2"), Wbs3: Find("3"), Wbs4: Find("4"),
            Wbs5: Find("5"), Wbs6: Find("6"), Wbs7: Find("7"),
            Package: Find("Package"),
            ActivityCode: Find("Activity Code"),
            Activity: Find("Activity"),
            OriginalDuration: Find("Original Duration"),
            Start: Find("Start"),
            Finish: Find("Finish"));
    }

    private static int FindColumn(IExcelSheet sheet, int headerRow, string label)
    {
        var row = sheet.Row(headerRow);
        for (var c = 1; c <= sheet.ColumnCount; c++)
        {
            var value = row.Cell(c).GetStringOrNull()?.Trim();
            if (string.Equals(value, label, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        return -1;
    }
}
