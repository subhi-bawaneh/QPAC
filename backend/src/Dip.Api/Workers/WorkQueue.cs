using System.Collections.Concurrent;
using System.Threading.Channels;
using Dip.Domain.Enums;

namespace Dip.Api.Workers;

// A unit of background work. Id is an ImportBatchId for ImportBatch and a ProjectId
// for Recalculate.
//
// Payload carries the uploaded workbook's bytes. Nothing is stored on disk or in the
// database any more, so the only copy of the file between the request and the worker
// is this array: an interrupted process loses it, which is why ImportWorker sweeps
// unfinished batches to Failed on startup rather than pretending they are still queued.
public sealed record WorkItem(WorkItemKind Kind, Guid Id, byte[]? Payload = null);

// Single-process queue between the request side and ImportWorker. Unbounded but
// deduplicated on (kind, id) — the payload is deliberately not part of the key, so a
// second enqueue of the same batch is a no-op rather than a second import.
public sealed class WorkQueue
{
    private readonly Channel<WorkItem> _channel = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly ConcurrentDictionary<(WorkItemKind Kind, Guid Id), byte> _pending = new();

    public int PendingCount => _pending.Count;

    public bool Enqueue(WorkItem item)
    {
        var key = (item.Kind, item.Id);
        if (!_pending.TryAdd(key, 0))
        {
            return false;
        }

        if (_channel.Writer.TryWrite(item))
        {
            return true;
        }

        _pending.TryRemove(key, out _);
        return false;
    }

    public bool EnqueueImport(Guid importBatchId, byte[] content) =>
        Enqueue(new WorkItem(WorkItemKind.ImportBatch, importBatchId, content));

    public bool EnqueueRecalculate(Guid projectId) => Enqueue(new WorkItem(WorkItemKind.Recalculate, projectId));

    public async Task<WorkItem> DequeueAsync(CancellationToken ct)
    {
        var item = await _channel.Reader.ReadAsync(ct);
        _pending.TryRemove((item.Kind, item.Id), out _);
        return item;
    }
}
