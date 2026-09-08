using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Folders.DownloadFile;

[Permission(Permissions.ReportsView)]
public sealed record DownloadFileQuery(Guid FileId) : IQuery<FileDownload>;

public sealed record FileDownload(string Name, byte[] Content);
