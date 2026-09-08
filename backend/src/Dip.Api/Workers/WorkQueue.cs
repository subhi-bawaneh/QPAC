using System.Collections.Concurrent;
using System.Threading.Channels;
using Dip.Domain.Enums;

namespace Dip.Api.Workers;

// Id is a FolderFileId for ImportFile and a ProjectId for Recalculate.
public sealed record WorkItem(WorkItemKind Kind, Guid Id);

// Single-process queue between the request/sync side and ImportWorker. Unbounded
// but deduplicated: enqueuing the same (kind, id) twice while it is still pending
// is a no-op, so a burst of Drive changes cannot pile up 40 recalculations.
public sealed class WorkQueue
{
    private readonly Channel<WorkItem> _channel = Channel.CreateUnbounded<WorkItem>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    private readonly ConcurrentDictionary<WorkItem, byte> _pending = new();

    public int PendingCount => _pending.Count;

    public bool Enqueue(WorkItem item)
    {
        if (!_pending.TryAdd(item, 0))
        {
            return false;
        }

        if (_channel.Writer.TryWrite(item))
        {
            return true;
        }

        _pending.TryRemove(item, out _);
        return false;
    }

    public bool EnqueueImport(Guid folderFileId) => Enqueue(new WorkItem(WorkItemKind.ImportFile, folderFileId));

    public bool EnqueueRecalculate(Guid projectId) => Enqueue(new WorkItem(WorkItemKind.Recalculate, projectId));

    public async Task<WorkItem> DequeueAsync(CancellationToken ct)
    {
        var item = await _channel.Reader.ReadAsync(ct);
        _pending.TryRemove(item, out _);
        return item;
    }
}
