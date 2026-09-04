using System.IO;

namespace Dip.Application.Abstractions;

public sealed record DriveEntry(
    string Id,
    string Name,
    string MimeType,
    DateTime? ModifiedTime,
    string? Md5Checksum,
    long? Size)
{
    public bool IsFolder => MimeType == "application/vnd.google-apps.folder";
}

public interface IDriveClient
{
    Task<IReadOnlyList<DriveEntry>> ListChildrenAsync(string folderId, CancellationToken ct);
    Task<Stream> DownloadAsync(string fileId, CancellationToken ct);
}
