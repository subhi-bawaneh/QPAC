using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Recalculation.RunRecalculationStep;

// One chunk of a recalculation. The caller repeats with the returned NextOffset
// until Done — see RecalculationService for why it is chunked.
[Permission(Permissions.ImportRun)]
public sealed record RunRecalculationStepCommand(
    Guid ProjectId,
    int Offset = 0,
    int Take = RecalculationService.DefaultChunkSize) : ICommand<RecalculationStepResult>;
