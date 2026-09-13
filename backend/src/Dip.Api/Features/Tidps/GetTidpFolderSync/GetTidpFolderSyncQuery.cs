using Dip.Api.Features.Tidps.SyncTidpFolder;
using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Tidps.GetTidpFolderSync;

// The outcome of one sync. The POST returns the plan — Added and Updated mean queued —
// and this returns the same rows with each file's real import status laid over them,
// which is where a workbook that would not parse finally shows up as Failed.
[Permission(Permissions.FilesManage)]
public sealed record GetTidpFolderSyncQuery(Guid ProjectId, Guid SyncId) : IQuery<TidpFolderSyncResult>;
