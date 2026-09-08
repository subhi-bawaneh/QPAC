using Dip.Api.Workers;
using Dip.Application.Abstractions;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Folders.TriggerDriveSync;

public sealed class TriggerDriveSyncHandler : ICommandHandler<TriggerDriveSyncCommand, TriggerSyncResult>
{
    private readonly DipDbContext _db;
    private readonly ISyncTrigger _trigger;
    private readonly WorkerState _state;

    public TriggerDriveSyncHandler(DipDbContext db, ISyncTrigger trigger, WorkerState state)
    {
        _db = db;
        _trigger = trigger;
        _state = state;
    }

    public async Task<TriggerSyncResult> Handle(TriggerDriveSyncCommand command, CancellationToken ct)
    {
        var exists = await _db.Projects.AnyAsync(p => p.Id == command.ProjectId, ct);
        if (!exists)
        {
            throw new KeyNotFoundException($"Project {command.ProjectId} not found");
        }

        // No root folder means no worker is reading the trigger; say so instead of
        // reporting a run that will never happen.
        var queued = _state.PollingEnabled && _trigger.Request(command.ProjectId);
        return new TriggerSyncResult(queued, _state.IsSyncRunning);
    }
}
