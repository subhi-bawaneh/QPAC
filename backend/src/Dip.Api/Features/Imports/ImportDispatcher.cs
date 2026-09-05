using System.Text.Json;
using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Imports;

// Routes an ImportBatch to the matching importer based on its Kind.
// Called by RunImportStep. Kept out of the feature slices so all Kind → importer
// mappings live in one place.
//
// Phase 3.5 runs the whole importer synchronously in a single step. The
// importers already chunk their own DB writes (2,000 rows per SaveChanges), so
// a MIDP / Aconex import completes in ~90s — well under the ASPMonster 10 min
// request timeout. If we ever need true resumable chunking, we swap this out
// for a staged approach that consumes ImportStagingRow one batch at a time.
public sealed class ImportDispatcher
{
    private readonly DipDbContext _db;
    private readonly BaselineImporter _baseline;
    private readonly PicklistImporter _picklist;
    private readonly ListsImporter _lists;
    private readonly TidpImporter _tidp;
    private readonly MidpImporter _midp;
    private readonly AconexHistoryImporter _aconex;

    public ImportDispatcher(
        DipDbContext db,
        BaselineImporter baseline,
        PicklistImporter picklist,
        ListsImporter lists,
        TidpImporter tidp,
        MidpImporter midp,
        AconexHistoryImporter aconex)
    {
        _db = db;
        _baseline = baseline;
        _picklist = picklist;
        _lists = lists;
        _tidp = tidp;
        _midp = midp;
        _aconex = aconex;
    }

    public async Task<ImportResult> RunAsync(
        Domain.Entities.ImportBatch batch,
        string filePath,
        string importedBy,
        CancellationToken ct)
    {
        try
        {
            var result = batch.Kind switch
            {
                ImportKind.Baseline => await _baseline.ImportAsync(batch.ProjectId, filePath, replace: true, ct),
                ImportKind.Picklists => await _picklist.ImportAsync(batch.ProjectId, filePath, ct),
                ImportKind.Lists => await _lists.ImportAsync(batch.ProjectId, filePath, ct),
                ImportKind.Tidp => await _tidp.ImportAsync(
                    batch.ProjectId, filePath, batch.Target, batch.FolderFileId, batch.Id, importedBy, ct),
                ImportKind.Midp => await _midp.ImportAsync(
                    batch.ProjectId, filePath, batch.Target, batch.FolderFileId, batch.Id, importedBy, ct),
                ImportKind.AconexHistory => await _aconex.ImportAsync(
                    batch.ProjectId, filePath, batch.Id, importedBy, ct),
                _ => throw new NotSupportedException($"Unsupported import kind: {batch.Kind}"),
            };

            batch.RowsRead = result.RowsRead;
            batch.RowsInserted = result.RowsInserted;
            batch.RowsUpdated = result.RowsUpdated;
            batch.RowsSkipped = result.RowsSkipped;
            batch.Completed = true;
            batch.Log = SerializeLog(result.Warnings, error: null);

            await UpdateFolderFileStateAsync(batch, ct);
            await _db.SaveChangesAsync(ct);
            return result;
        }
        catch (Exception ex)
        {
            batch.Completed = false;
            batch.Log = SerializeLog(warnings: Array.Empty<string>(), error: ex.Message);
            await UpdateFolderFileStateAsync(batch, ct, failed: true);
            await _db.SaveChangesAsync(ct);
            throw;
        }
    }

    private async Task UpdateFolderFileStateAsync(
        Domain.Entities.ImportBatch batch, CancellationToken ct, bool failed = false)
    {
        if (batch.FolderFileId is null) return;
        var file = await _db.FolderFiles.FirstOrDefaultAsync(f => f.Id == batch.FolderFileId, ct);
        if (file is null) return;
        file.State = failed ? ImportState.Failed : ImportState.Imported;
        file.LastImportBatchId = batch.Id;
    }

    private static string SerializeLog(IReadOnlyCollection<string> warnings, string? error) =>
        JsonSerializer.Serialize(new { warnings, error });
}
