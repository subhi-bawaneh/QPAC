using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Reads `Picklists` sheet from TIDP.xlsx / MIDP.xlsx (or the standalone
// PickLists.xlsx). The sheet is laid out horizontally: each FIELD occupies
// two columns (Code, Description); FIELD labels sit two rows above the data.
//
// The reader locates each FIELD header row by searching for the labels
// (FIELD 01 - PROJECT CODE, FIELD 04 - DOCUMENT TYPES, FIELD 05 - DISCIPLINE, …)
// so it works regardless of small row shifts between workbooks.
public sealed class PicklistImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    public PicklistImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    // FIELD label prefix -> matching PicklistField enum.
    // Only fields the project actively uses are included; unknowns are skipped
    // with a warning so the user notices.
    private static readonly (string Prefix, PicklistField Field)[] FieldMap =
    [
        ("FIELD 01",  PicklistField.Project),
        ("FIELD 02",  PicklistField.Originator),
        ("FIELD 03",  PicklistField.Contract),
        ("FIELD 04",  PicklistField.DocType),
        ("FIELD 05",  PicklistField.Discipline),
        ("FIELD 06",  PicklistField.Zone),
        ("FIELD 07",  PicklistField.Building),
        ("FIELD 08A", PicklistField.DrawingType),
        ("FIELD 08B", PicklistField.Level),
    ];

    public async Task<ImportResult> ImportAsync(Guid projectId, string filePath, CancellationToken ct)
    {
        using var wb = _reader.Open(filePath);
        // Different files use different casings for the same sheet:
        //   TIDP-STL / MIDP -> "Picklists"
        //   Standalone      -> "Pick_Lists"
        IExcelSheet? sheet = null;
        foreach (var candidate in new[] { "Picklists", "PickLists", "Pick_Lists" })
        {
            if (wb.TryGetSheet(candidate, out sheet) && sheet is not null) break;
        }
        if (sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'Picklists' sheet in workbook" });
        }

        // Find the row that holds the FIELD labels (contains "FIELD 01" anywhere).
        var labelRow = sheet.FindRowContainingAnywhere("FIELD 01")
            ?? throw new InvalidOperationException("Cannot locate 'FIELD 01' label in Picklists sheet");
        // Data begins two rows below the labels (labels, sub-header abbreviations, then data).
        var firstDataRow = labelRow + 2;

        var warnings = new List<string>();
        var inserted = 0;
        var updated = 0;
        var read = 0;

        // Load existing items in one shot for fast lookup.
        var existingRows = await _db.PicklistItems
            .Where(p => p.ProjectId == projectId)
            .ToListAsync(ct);
        var existing = existingRows.ToDictionary(p => (p.Field, p.Code), StringComparerTuple.OrdinalIgnoreCase);

        // Scan the label row for each FIELD; the value column is the same column
        // as the label; the description column is the immediate next.
        var lastCol = sheet.ColumnCount;
        var labelHeader = sheet.Row(labelRow);
        for (var col = 1; col <= lastCol; col++)
        {
            var label = labelHeader.Cell(col).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(label))
            {
                continue;
            }

            var mapping = FieldMap.FirstOrDefault(m => label.StartsWith(m.Prefix, StringComparison.OrdinalIgnoreCase));
            if (mapping.Prefix is null)
            {
                continue;
            }

            var codeCol = col;
            var descCol = col + 1;
            var lastRow = sheet.RowCount;
            var sortOrder = 0;

            for (var r = firstDataRow; r <= lastRow; r++)
            {
                var codeCell = sheet.Row(r).Cell(codeCol);
                var descCell = sheet.Row(r).Cell(descCol);
                var code = codeCell.GetStringOrNull()?.Trim();
                var description = descCell.GetStringOrNull()?.Trim() ?? string.Empty;
                if (string.IsNullOrEmpty(code))
                {
                    continue;
                }

                read++;
                sortOrder++;

                if (existing.TryGetValue((mapping.Field, code), out var current))
                {
                    if (current.Description != description || current.SortOrder != sortOrder)
                    {
                        current.Description = description;
                        current.SortOrder = sortOrder;
                        updated++;
                    }
                }
                else
                {
                    _db.PicklistItems.Add(new PicklistItem
                    {
                        ProjectId = projectId,
                        Field = mapping.Field,
                        Code = code,
                        Description = description,
                        SortOrder = sortOrder,
                    });
                    inserted++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, 0, warnings);
    }
}

// Custom equality comparer for tuple keys so lookups ignore case on Code.
internal static class StringComparerTuple
{
    public static IEqualityComparer<(Dip.Domain.Enums.PicklistField Field, string Code)> OrdinalIgnoreCase { get; } =
        new PicklistKeyComparer();

    private sealed class PicklistKeyComparer : IEqualityComparer<(Dip.Domain.Enums.PicklistField Field, string Code)>
    {
        public bool Equals((Dip.Domain.Enums.PicklistField Field, string Code) x, (Dip.Domain.Enums.PicklistField Field, string Code) y) =>
            x.Field == y.Field && string.Equals(x.Code, y.Code, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((Dip.Domain.Enums.PicklistField Field, string Code) obj) =>
            HashCode.Combine((int)obj.Field, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Code));
    }
}
