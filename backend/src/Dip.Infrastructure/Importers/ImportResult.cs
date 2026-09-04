namespace Dip.Infrastructure.Importers;

// What every importer returns. Import handlers turn this into an ImportBatch row.
public sealed record ImportResult(
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int RowsSkipped,
    IReadOnlyList<string> Warnings)
{
    public static ImportResult Empty { get; } = new(0, 0, 0, 0, Array.Empty<string>());
}
