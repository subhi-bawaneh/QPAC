namespace Dip.Infrastructure.Importers;

// What every importer returns. Import handlers turn this into an ImportBatch row.
//
// RowsDuplicate is the Aconex append's: lines the project already held, dropped and
// counted. It is the figure that tells an operator a re-upload of an overlapping
// export did nothing, which is the whole point of appending rather than replacing.
public sealed record ImportResult(
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int RowsSkipped,
    IReadOnlyList<string> Warnings,
    int RowsDuplicate = 0)
{
    public static ImportResult Empty { get; } = new(0, 0, 0, 0, Array.Empty<string>());
}
