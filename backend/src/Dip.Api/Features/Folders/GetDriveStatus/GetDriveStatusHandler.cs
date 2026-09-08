using Dip.Api.Workers;
using Dip.Application.Abstractions;

namespace Dip.Api.Features.Folders.GetDriveStatus;

public sealed class GetDriveStatusHandler : IQueryHandler<GetDriveStatusQuery, DriveStatusDto>
{
    private readonly WorkerState _state;

    public GetDriveStatusHandler(WorkerState state) => _state = state;

    public Task<DriveStatusDto> Handle(GetDriveStatusQuery query, CancellationToken ct) =>
        Task.FromResult(new DriveStatusDto(
            _state.IsSyncRunning,
            _state.LastRunStartedAt,
            _state.LastRunFinishedAt,
            _state.LastRunError,
            _state.NextRunAt,
            _state.QueuedImports));
}
