using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Reads the `Pick_Lists` sheet (also named `Picklists` inside TIDP/MIDP workbooks).
// The sheet is laid out horizontally: group labels in row 4, column headers in row 5,
// data from row 6 down. Two kinds of list live side by side —
//   * numbering lists, a code column plus a description column ("Ab. 05" / "5 - Discipline")
//   * value lists, a single column ("Authoring Software", "Author", …)
//
// Everything is located by its row-5 header text rather than by a column number, so
// the reader survives columns being inserted, and the `8C - Sequence Number` column —
// which holds prose, not codes — is simply not in the map.
//
// Soft-deleted codes are never resurrected (refactor-plan § 3 R9): they are counted as
// skipped and listed in the warnings.
public sealed class PicklistImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    public PicklistImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    // Header text of the code column -> the list it feeds, and the header the
    // description column must carry for the pair to be read as code + description.
    private sealed record ListSpec(PicklistField Field, string? DescriptionHeader);

    private static readonly IReadOnlyDictionary<string, ListSpec> Lists =
        new Dictionary<string, ListSpec>(StringComparer.OrdinalIgnoreCase)
        {
            ["Ab. 01"] = new(PicklistField.Project, "1 - Project Name"),
            ["Ab. 02"] = new(PicklistField.Originator, "2 - Originator Name"),
            ["Ab. 03"] = new(PicklistField.Contract, "3 - Contract Reference"),
            ["Ab. 04"] = new(PicklistField.DocType, "4 - Document Type"),
            ["Ab. 05"] = new(PicklistField.Discipline, "5 - Discipline"),
            ["Ab. 06"] = new(PicklistField.Zone, "6 - Area/Zone"),
            ["Ab. 07"] = new(PicklistField.Building, "7 - Venue/Building"),
            ["Ab. 08A"] = new(PicklistField.DrawingType, "8A - Drawing Type"),
            ["Ab. 08B"] = new(PicklistField.Level, "8B - Level"),
            ["Authoring Software"] = new(PicklistField.AuthoringSoftware, null),
            ["File/Exchange Format"] = new(PicklistField.ExchangeFormat, null),
            ["Scope Area"] = new(PicklistField.ScopeArea, null),
            ["Code"] = new(PicklistField.SuitabilityCode, "Status"),
            ["Scale"] = new(PicklistField.Scale, null),
            ["Classification ID"] = new(PicklistField.Classification, "Classification"),
            ["Corporate Discipline"] = new(PicklistField.CorporateDiscipline, null),
            ["Author"] = new(PicklistField.Author, null),
        };

    public async Task<ImportResult> ImportAsync(Guid projectId, Stream content, CancellationToken ct)
    {
        using var wb = _reader.Open(content);
        IExcelSheet? sheet = null;
        foreach (var candidate in new[] { "Pick_Lists", "Picklists", "PickLists" })
        {
            if (wb.TryGetSheet(candidate, out sheet) && sheet is not null) break;
        }
        if (sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'Pick_Lists' sheet in workbook" });
        }

        // The group labels ("FIELD 01 - PROJECT CODE") sit one row above the column
        // headers, and the data starts one row below those.
        var labelRow = sheet.FindRowContainingAnywhere("FIELD 01")
            ?? throw new InvalidOperationException("Cannot locate 'FIELD 01' label in the Picklists sheet");
        var headerRow = labelRow + 1;
        var firstDataRow = labelRow + 2;

        var existingRows = await _db.PicklistItems
            .Where(p => p.ProjectId == projectId)
            .ToListAsync(ct);
        var existing = existingRows.ToDictionary(
            p => (p.Field, p.Code), StringComparerTuple.OrdinalIgnoreCase);

        var warnings = new List<string>();
        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;

        var header = sheet.Row(headerRow);
        var lastCol = sheet.ColumnCount;
        var lastRow = sheet.RowCount;
        var consumed = new HashSet<int>();

        for (var col = 1; col <= lastCol; col++)
        {
            if (consumed.Contains(col)) continue;

            var title = header.Cell(col).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(title) || !Lists.TryGetValue(title, out var spec)) continue;

            var descCol = 0;
            if (spec.DescriptionHeader is not null)
            {
                var next = header.Cell(col + 1).GetStringOrNull()?.Trim();
                if (string.Equals(next, spec.DescriptionHeader, StringComparison.OrdinalIgnoreCase))
                {
                    descCol = col + 1;
                    consumed.Add(descCol);
                }
            }

            var sortOrder = 0;
            for (var r = firstDataRow; r <= lastRow; r++)
            {
                // Scope Area and Zone carry trailing spaces in the sheet.
                var code = sheet.Row(r).Cell(col).GetStringOrNull()?.Trim();
                if (string.IsNullOrEmpty(code)) continue;

                var description = descCol == 0
                    ? string.Empty
                    : sheet.Row(r).Cell(descCol).GetStringOrNull()?.Trim() ?? string.Empty;

                read++;
                sortOrder++;

                if (existing.TryGetValue((spec.Field, code), out var current))
                {
                    if (current.IsDeleted)
                    {
                        // The operator removed this code; a re-import must not bring it back.
                        skipped++;
                        warnings.Add($"{spec.Field} '{code}' is deleted and was skipped");
                        continue;
                    }

                    if (current.Description != description || current.SortOrder != sortOrder)
                    {
                        current.Description = description;
                        current.SortOrder = sortOrder;
                        updated++;
                    }
                }
                else
                {
                    var item = new PicklistItem
                    {
                        ProjectId = projectId,
                        Field = spec.Field,
                        Code = code,
                        Description = description,
                        SortOrder = sortOrder,
                    };
                    _db.PicklistItems.Add(item);
                    existing[(spec.Field, code)] = item;
                    inserted++;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }
}

// Custom equality comparer for tuple keys so lookups ignore case on Code.
internal static class StringComparerTuple
{
    public static IEqualityComparer<(PicklistField Field, string Code)> OrdinalIgnoreCase { get; } =
        new PicklistKeyComparer();

    private sealed class PicklistKeyComparer : IEqualityComparer<(PicklistField Field, string Code)>
    {
        public bool Equals((PicklistField Field, string Code) x, (PicklistField Field, string Code) y) =>
            x.Field == y.Field && string.Equals(x.Code, y.Code, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((PicklistField Field, string Code) obj) =>
            HashCode.Combine((int)obj.Field, StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Code));
    }
}
