using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Imports.GetImportStatus;

[Permission(Permissions.ReportsView)]
public sealed record GetImportStatusQuery(Guid ImportBatchId) : IQuery<ImportBatchSummary>;
