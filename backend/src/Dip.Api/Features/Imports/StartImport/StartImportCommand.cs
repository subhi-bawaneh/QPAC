using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Imports.StartImport;

// Creates the ImportBatch row and returns its Id. Does NOT execute the
// import — the client immediately follows up with RunImportStep so the
// long-running work sits in a separate request (matches PLAN.md § 6).
[Permission(Permissions.ImportRun)]
public sealed record StartImportCommand(
    Guid ProjectId,
    Guid FolderFileId,
    ImportKind Kind,
    DataTarget Target) : ICommand<StartImportResult>;
