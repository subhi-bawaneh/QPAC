using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.TriggerDriveSync;

// "Sync now". The walk itself belongs to DriveSyncWorker; this only asks for it.
[Permission(Permissions.DriveSync)]
public sealed record TriggerDriveSyncCommand(Guid ProjectId) : ICommand<TriggerSyncResult>;
