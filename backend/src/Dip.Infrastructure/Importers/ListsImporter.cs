using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Importers;

// Reads the `Lists` sheet in Tracker.xlsx (columns "Aconex Status" + "Status")
// and upserts StatusMapping rows for a project. Existing entries are updated;
// legacy entries from MIDP.xlsx!LISTS are left untouched (IsLegacy=true).
//
// Sheet layout:
//   Row headers on the "Aconex Status" row (found by search).
//   Data rows below; blanks stop parsing.
public sealed class ListsImporter
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;

    public ListsImporter(DipDbContext db, IExcelReader reader)
    {
        _db = db;
        _reader = reader;
    }

    public async Task<ImportResult> ImportAsync(Guid projectId, string filePath, CancellationToken ct)
    {
        using var wb = _reader.Open(filePath);
        if (!wb.TryGetSheet("Lists", out var sheet) || sheet is null)
        {
            return new ImportResult(0, 0, 0, 0, new[] { "No 'Lists' sheet in workbook" });
        }

        var headerRow = sheet.FindRowContainingAnywhere("Aconex Status")
            ?? throw new InvalidOperationException("Cannot find 'Aconex Status' header in Lists sheet");

        var (aconexCol, statusCol) = LocateColumns(sheet, headerRow);
        var lastRow = sheet.RowCount;

        var existingRows = await _db.StatusMappings
            .Where(m => m.ProjectId == projectId)
            .ToListAsync(ct);
        // Case-insensitive lookup, but the seeder deliberately inserts both
        // "No Longer in Use" (legacy) and "No Longer In Use" (modern Aconex).
        // Group + take-first keeps both rows queryable via one canonical key.
        var existing = existingRows
            .GroupBy(m => m.AconexStatus, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var read = 0;
        var inserted = 0;
        var updated = 0;
        var skipped = 0;
        var warnings = new List<string>();

        for (var r = headerRow + 1; r <= lastRow; r++)
        {
            var aconex = sheet.Row(r).Cell(aconexCol).GetStringOrNull()?.Trim();
            var statusText = sheet.Row(r).Cell(statusCol).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(aconex))
            {
                continue;
            }

            read++;

            var unified = ParseUnifiedStatus(statusText);
            if (unified is null)
            {
                warnings.Add($"Row {r}: unknown unified status '{statusText}' for '{aconex}' - skipped");
                skipped++;
                continue;
            }

            if (existing.TryGetValue(aconex, out var current))
            {
                if (current.Status != unified.Value)
                {
                    current.Status = unified.Value;
                    updated++;
                }
            }
            else
            {
                _db.StatusMappings.Add(new StatusMapping
                {
                    ProjectId = projectId,
                    AconexStatus = aconex,
                    Status = unified.Value,
                    IsLegacy = false,
                });
                inserted++;
            }
        }

        await _db.SaveChangesAsync(ct);
        return new ImportResult(read, inserted, updated, skipped, warnings);
    }

    // Accepts "Under Review" (with the space) as well as "UnderReview".
    private static UnifiedStatus? ParseUnifiedStatus(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var normalized = text.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
        return Enum.TryParse<UnifiedStatus>(normalized, ignoreCase: true, out var unified) ? unified : null;
    }

    private static (int AconexCol, int StatusCol) LocateColumns(IExcelSheet sheet, int headerRow)
    {
        int aconex = -1, status = -1;
        var row = sheet.Row(headerRow);
        for (var c = 1; c <= sheet.ColumnCount; c++)
        {
            var value = row.Cell(c).GetStringOrNull()?.Trim();
            if (string.Equals(value, "Aconex Status", StringComparison.OrdinalIgnoreCase)) aconex = c;
            else if (string.Equals(value, "Status", StringComparison.OrdinalIgnoreCase)) status = c;
        }
        if (aconex < 0 || status < 0)
        {
            throw new InvalidOperationException("Lists sheet is missing 'Aconex Status' or 'Status' column");
        }
        return (aconex, status);
    }
}
