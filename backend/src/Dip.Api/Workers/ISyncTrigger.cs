using System.Threading.Channels;

namespace Dip.Api.Workers;

// On-demand "sync now". The command handler writes a project id, DriveSyncWorker
// reads it. A request arriving mid-run sets the worker's run-again flag rather
// than starting a second concurrent walk of the Drive tree.
public interface ISyncTrigger
{
    bool Request(Guid projectId);
}

public sealed class SyncTrigger : ISyncTrigger
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public bool Request(Guid projectId) => _channel.Writer.TryWrite(projectId);

    public ValueTask<Guid> WaitAsync(CancellationToken ct) => _channel.Reader.ReadAsync(ct);
}
