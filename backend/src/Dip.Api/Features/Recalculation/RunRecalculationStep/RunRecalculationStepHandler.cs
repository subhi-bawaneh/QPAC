using Dip.Application.Abstractions;

namespace Dip.Api.Features.Recalculation.RunRecalculationStep;

public sealed class RunRecalculationStepHandler
    : ICommandHandler<RunRecalculationStepCommand, RecalculationStepResult>
{
    private readonly RecalculationService _service;

    public RunRecalculationStepHandler(RecalculationService service) => _service = service;

    public Task<RecalculationStepResult> Handle(
        RunRecalculationStepCommand command, CancellationToken ct) =>
        _service.RunStepAsync(command.ProjectId, command.Offset, command.Take, ct);
}
