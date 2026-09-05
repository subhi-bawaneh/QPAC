using Dip.Application.Abstractions;
using Dip.Application.Authorization;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Imports.ListImportBatches;

[Permission(Permissions.ReportsView)]
public sealed record ListImportBatchesQuery(
    Guid ProjectId,
    ImportKind? Kind = null,
    Guid? FolderFileId = null,
    int Take = 50) : IQuery<IReadOnlyCollection<ImportBatchSummary>>;
