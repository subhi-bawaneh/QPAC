using System.Text.Json;
using Dip.Api.Features.Imports;
using Dip.Api.Hubs;
using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Workers;

// Imports one workbook straight from its FileBlob. Which sheets run and which
// layer they land in is refactor-plan § 3 R3/R4/R5:
//   * Drive-sourced planning data always goes to Draft — Drive never writes Live.
//   * Upload-sourced planning data follows the folder's target.
//   * Aconex history, baseline, picklists and status lists are always Live.
public sealed class FileImportService
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;
    private readonly WorkQueue _queue;
    private readonly ISyncNotifier _notifier;
    private readonly TidpImporter _tidp;
    private readonly MidpImporter _midp;
    private readonly AconexHistoryImporter _aconex;
    private readonly BaselineImporter _baseline;
    private readonly PicklistImporter _picklists;
    private readonly ListsImporter _lists;
    private readonly ILogger<FileImportService> _logger;

    public FileImportService(
        DipDbContext db,
        IExcelReader reader,
        WorkQueue queue,
        ISyncNotifier notifier,
        TidpImporter tidp,
        MidpImporter midp,
        AconexHistoryImporter aconex,
        BaselineImporter baseline,
        PicklistImporter picklists,
        ListsImporter lists,
        ILogger<FileImportService> logger)
    {
        _db = db;
        _reader = reader;
        _queue = queue;
        _notifier = notifier;
        _tidp = tidp;
        _midp = midp;
        _aconex = aconex;
        _baseline = baseline;
        _picklists = picklists;
        _lists = lists;
        _logger = logger;
    }

    public const string UnrecognisedWorkbook = "Unrecognised workbook name";

    public static ImportKind KindOf(FileKind kind) => kind switch
    {
        FileKind.Tidp => ImportKind.Tidp,
        FileKind.Midp => ImportKind.Midp,
        FileKind.AconexHistory => ImportKind.AconexHistory,
        FileKind.Baseline => ImportKind.Baseline,
        FileKind.Picklists => ImportKind.Picklists,
        FileKind.Lists => ImportKind.Lists,
        _ => throw new NotSupportedException($"No import kind for {kind}"),
    };

    // Planning data (TIDP/MIDP rows) is the only content whose layer is a choice.
    public static DataTarget LayerFor(FileKind kind, FileSource source, DataTarget folderTarget) =>
        kind is FileKind.Tidp or FileKind.Midp
            ? (source == FileSource.Drive ? DataTarget.Draft : folderTarget)
            : DataTarget.Live;

    public async Task ImportAsync(Guid folderFileId, CancellationToken ct)
    {
        var file = await _db.FolderFiles
            .Include(f => f.Folder)
            .FirstOrDefaultAsync(f => f.Id == folderFileId, ct);
        if (file is null || file.IsDeleted || file.Folder is null)
        {
            return;
        }

        var folder = file.Folder;
        var blob = await _db.FileBlobs.AsNoTracking()
            .FirstOrDefaultAsync(b => b.FolderFileId == folderFileId, ct);
        if (blob is null)
        {
            await FailAsync(folder.ProjectId, file.Id, file.FolderId, batchId: null,
                "The workbook has no stored content; sync it from Drive or upload it again", ct);
            return;
        }

        if (file.Kind == FileKind.Unknown)
        {
            await FailAsync(folder.ProjectId, file.Id, file.FolderId, batchId: null, UnrecognisedWorkbook, ct);
            return;
        }

        var layer = LayerFor(file.Kind, file.ContentSource, folder.Target);
        var batch = new ImportBatch
        {
            ProjectId = folder.ProjectId,
            Kind = KindOf(file.Kind),
            Target = layer,
            FolderFileId = file.Id,
            FileName = file.Name,
            ImportedAt = DateTime.UtcNow,
            ImportedBy = "worker",
            Completed = false,
        };
        _db.ImportBatches.Add(batch);
        await _db.SaveChangesAsync(ct);

        await _notifier.FileImportStartedAsync(folder.ProjectId, file.Id);

        try
        {
            var results = await RunImportersAsync(folder.ProjectId, file, layer, batch, blob.Content, ct);

            batch.RowsRead = results.Sum(r => r.RowsRead);
            batch.RowsInserted = results.Sum(r => r.RowsInserted);
            batch.RowsUpdated = results.Sum(r => r.RowsUpdated);
            batch.RowsSkipped = results.Sum(r => r.RowsSkipped);
            batch.Completed = true;
            batch.Log = SerializeLog(results.SelectMany(r => r.Warnings).ToList(), error: null);

            file.State = ImportState.Imported;
            file.ImportError = null;
            file.LastImportBatchId = batch.Id;
            file.LastImportedAt = batch.ImportedAt;
            await _db.SaveChangesAsync(ct);

            _queue.EnqueueRecalculate(folder.ProjectId);
            await _notifier.FileImportedAsync(
                folder.ProjectId, file.Id, folder.Id, ImportBatchSummary.From(batch));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Import failed for {File}", file.Name);
            await FailAsync(folder.ProjectId, file.Id, file.FolderId, batch.Id, ex.Message, ct);
        }
    }

    private async Task<List<ImportResult>> RunImportersAsync(
        Guid projectId, FolderFile file, DataTarget layer, ImportBatch batch, byte[] content, CancellationToken ct)
    {
        var sheets = SheetNames(content);
        var results = new List<ImportResult>();

        switch (file.Kind)
        {
            case FileKind.Tidp:
                results.Add(await _tidp.ImportAsync(
                    projectId, Open(content), layer, file.Id, batch.Id, batch.ImportedBy, ct));
                break;

            case FileKind.Midp:
                results.Add(await _midp.ImportAsync(
                    projectId, Open(content), layer, file.Id, batch.Id, batch.ImportedBy, ct));
                // Aconex history rides along in the MIDP workbook and is always Live.
                if (sheets.Contains("Aconex History") || sheets.Contains("SHD_History"))
                {
                    results.Add(await _aconex.ImportAsync(
                        projectId, Open(content), batch.Id, batch.ImportedBy, ct));
                }
                break;

            case FileKind.AconexHistory:
                results.Add(await _aconex.ImportAsync(
                    projectId, Open(content), batch.Id, batch.ImportedBy, ct));
                if (sheets.Contains("Baseline") || sheets.Contains("ENG_BL"))
                {
                    results.Add(await _baseline.ImportAsync(projectId, Open(content), replace: true, ct));
                }
                if (sheets.Contains("Lists"))
                {
                    results.Add(await _lists.ImportAsync(projectId, Open(content), ct));
                }
                break;

            case FileKind.Baseline:
                results.Add(await _baseline.ImportAsync(projectId, Open(content), replace: true, ct));
                break;

            case FileKind.Picklists:
                results.Add(await _picklists.ImportAsync(projectId, Open(content), ct));
                break;

            case FileKind.Lists:
                results.Add(await _lists.ImportAsync(projectId, Open(content), ct));
                break;

            default:
                throw new NotSupportedException($"No sheet routing for {file.Kind}");
        }

        return results;
    }

    private HashSet<string> SheetNames(byte[] content)
    {
        using var wb = _reader.Open(Open(content));
        return wb.SheetNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // A fresh read-only view over the same array per importer: ClosedXML consumes
    // the stream, and nothing ever touches the file system (decision D2).
    private static MemoryStream Open(byte[] content) => new(content, writable: false);

    // Written straight to the database rather than through the change tracker: a
    // failed importer may have left half-built entities tracked, and the failure
    // record must land regardless.
    private async Task FailAsync(
        Guid projectId, Guid fileId, Guid folderId, Guid? batchId, string error, CancellationToken ct)
    {
        var message = error.Length > 2000 ? error[..2000] : error;

        await _db.FolderFiles
            .Where(f => f.Id == fileId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(f => f.State, ImportState.Failed)
                .SetProperty(f => f.ImportError, message), ct);

        if (batchId is not null)
        {
            var log = SerializeLog(Array.Empty<string>(), message);
            await _db.ImportBatches
                .Where(b => b.Id == batchId.Value)
                .ExecuteUpdateAsync(set => set
                    .SetProperty(b => b.Completed, false)
                    .SetProperty(b => b.Log, log), ct);
        }

        await _notifier.FileFailedAsync(projectId, fileId, folderId, message);
    }

    private static string SerializeLog(IReadOnlyCollection<string> warnings, string? error) =>
        JsonSerializer.Serialize(new { warnings, error });
}
