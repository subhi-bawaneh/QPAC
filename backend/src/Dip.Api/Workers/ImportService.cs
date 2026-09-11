using System.Text.Json;
using Dip.Application.Abstractions;
using Dip.Api.Features.Imports;
using Dip.Api.Hubs;
using Dip.Domain.Enums;
using Dip.Infrastructure.Importers;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Workers;

// Runs one ImportBatch against the bytes the upload handed to the queue.
//
// The kind comes from the page the operator uploaded from, so there is no filename
// guessing: a file named CHECKLIST.xlsx can no longer be routed to the Lists importer
// because it happens to contain the word LIST.
//
// A workbook can still carry more than one sheet worth importing — the Aconex export
// has Baseline and Lists tabs — and each importer's counters are recorded separately in
// the log as well as summed on the batch. Summing alone is what made the old batch row
// read "26,622 rows" for an event log holding 25,246.
public sealed class ImportService
{
    private readonly DipDbContext _db;
    private readonly IExcelReader _reader;
    private readonly TidpImporter _tidp;
    private readonly AconexHistoryImporter _aconex;
    private readonly BaselineImporter _baseline;
    private readonly PicklistImporter _picklists;
    private readonly ListsImporter _lists;
    private readonly ISyncNotifier _notifier;
    private readonly ILogger<ImportService> _logger;

    public ImportService(
        DipDbContext db,
        IExcelReader reader,
        TidpImporter tidp,
        AconexHistoryImporter aconex,
        BaselineImporter baseline,
        PicklistImporter picklists,
        ListsImporter lists,
        ISyncNotifier notifier,
        ILogger<ImportService> logger)
    {
        _db = db;
        _reader = reader;
        _tidp = tidp;
        _aconex = aconex;
        _baseline = baseline;
        _picklists = picklists;
        _lists = lists;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task RunAsync(Guid batchId, byte[] content, CancellationToken ct)
    {
        var batch = await _db.ImportBatches.FirstOrDefaultAsync(b => b.Id == batchId, ct);
        if (batch is null)
        {
            _logger.LogWarning("Import batch {Id} no longer exists", batchId);
            return;
        }

        batch.Status = ImportBatchStatus.Running;
        await _db.SaveChangesAsync(ct);
        await _notifier.ImportStartedAsync(batch.ProjectId, batch.Id);

        try
        {
            var results = await RunImportersAsync(batch, content, ct);

            batch.RowsRead = results.Sum(r => r.Result.RowsRead);
            batch.RowsInserted = results.Sum(r => r.Result.RowsInserted);
            batch.RowsUpdated = results.Sum(r => r.Result.RowsUpdated);
            batch.RowsSkipped = results.Sum(r => r.Result.RowsSkipped);
            batch.RowsDuplicate = results.Sum(r => r.Result.RowsDuplicate);
            batch.Log = SerializeLog(results, error: null);
            batch.Status = ImportBatchStatus.Completed;

            if (batch.TidpFileId is { } tidpFileId)
            {
                await _db.TidpFiles.Where(t => t.Id == tidpFileId)
                    .ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, TidpFileStatus.Imported), ct);
            }

            await _db.SaveChangesAsync(ct);
            await _notifier.ImportFinishedAsync(
                batch.ProjectId, batch.Id, ImportBatchSummary.From(batch));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Import failed for {File}", batch.FileName);
            await FailAsync(batch, ex.Message, ct);
        }
    }

    private async Task<IReadOnlyList<(string Importer, ImportResult Result)>> RunImportersAsync(
        Domain.Entities.ImportBatch batch, byte[] content, CancellationToken ct)
    {
        var sheets = SheetNames(content);
        var results = new List<(string, ImportResult)>();

        switch (batch.Kind)
        {
            case ImportKind.Tidp:
                results.Add(("Tidp", await _tidp.ImportAsync(
                    batch.ProjectId, Open(content),
                    batch.TidpFileId ?? throw new InvalidOperationException("TIDP batch has no file"),
                    batch.UploadedBy, ct)));
                break;

            case ImportKind.AconexHistory:
                results.Add(("AconexHistory", await _aconex.ImportAsync(
                    batch.ProjectId, Open(content), batch.Id, batch.UploadedBy, ct)));

                // The export carries these tabs; importing them is free and keeps the
                // baseline and the status mappings in step with the events.
                if (sheets.Contains("Baseline") || sheets.Contains("ENG_BL"))
                {
                    results.Add(("Baseline", await _baseline.ImportAsync(
                        batch.ProjectId, Open(content), replace: true, ct)));
                }
                if (sheets.Contains("Lists"))
                {
                    results.Add(("Lists", await _lists.ImportAsync(batch.ProjectId, Open(content), ct)));
                }
                break;

            case ImportKind.Baseline:
                results.Add(("Baseline", await _baseline.ImportAsync(
                    batch.ProjectId, Open(content), replace: true, ct)));
                break;

            case ImportKind.Picklists:
                results.Add(("Picklists", await _picklists.ImportAsync(batch.ProjectId, Open(content), ct)));
                break;

            case ImportKind.Lists:
                results.Add(("Lists", await _lists.ImportAsync(batch.ProjectId, Open(content), ct)));
                break;

            default:
                throw new NotSupportedException($"No sheet routing for {batch.Kind}");
        }

        return results;
    }

    private async Task FailAsync(Domain.Entities.ImportBatch batch, string error, CancellationToken ct)
    {
        // ExecuteUpdate rather than the change tracker: a failed importer may have left
        // half-built entities tracked, and saving them would be worse than the failure.
        var log = SerializeLog(Array.Empty<(string, ImportResult)>(), error);
        await _db.ImportBatches.Where(b => b.Id == batch.Id)
            .ExecuteUpdateAsync(
                u => u.SetProperty(b => b.Status, ImportBatchStatus.Failed)
                      .SetProperty(b => b.Log, log),
                ct);

        if (batch.TidpFileId is { } tidpFileId)
        {
            var message = error.Length > 2000 ? error.Substring(0, 2000) : error;
            await _db.TidpFiles.Where(t => t.Id == tidpFileId)
                .ExecuteUpdateAsync(
                    u => u.SetProperty(t => t.Status, TidpFileStatus.Failed)
                          .SetProperty(t => t.Error, message),
                    ct);
        }

        await _notifier.ImportFailedAsync(batch.ProjectId, batch.Id, error);
    }

    private IReadOnlyCollection<string> SheetNames(byte[] content)
    {
        using var workbook = _reader.Open(Open(content));
        return workbook.SheetNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Stream Open(byte[] content) => new MemoryStream(content, writable: false);

    private static string SerializeLog(
        IReadOnlyList<(string Importer, ImportResult Result)> results, string? error) =>
        JsonSerializer.Serialize(new
        {
            // Per importer, not only summed: one workbook can feed three of them, and a
            // single "rows read" figure over the three means nothing to an operator.
            importers = results.Select(r => new
            {
                importer = r.Importer,
                read = r.Result.RowsRead,
                inserted = r.Result.RowsInserted,
                updated = r.Result.RowsUpdated,
                skipped = r.Result.RowsSkipped,
                duplicate = r.Result.RowsDuplicate,
            }),
            warnings = results.SelectMany(r => r.Result.Warnings).ToList(),
            error,
        });
}
