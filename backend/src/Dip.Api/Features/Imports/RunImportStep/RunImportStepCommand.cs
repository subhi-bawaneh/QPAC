using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Imports.RunImportStep;

// Executes one processing step for an ImportBatch. Today each call runs the
// whole importer inline (the importer chunks its own DB writes to keep the
// request under 10 min). The two-step Start/Step protocol is preserved so
// the UI can render a progress spinner without special-casing kinds.
[Permission(Permissions.ImportRun)]
public sealed record RunImportStepCommand(Guid ImportBatchId) : ICommand<RunStepResult>;
