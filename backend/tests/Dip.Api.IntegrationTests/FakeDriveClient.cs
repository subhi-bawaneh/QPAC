using System.IO;
using Dip.Application.Abstractions;

namespace Dip.Api.IntegrationTests;

// In-memory Drive: the sync tests drive it directly instead of talking to Google.
internal sealed class FakeDriveClient : IDriveClient
{
    private readonly Dictionary<string, List<DriveEntry>> _children = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> _content = new(StringComparer.Ordinal);

    public int Downloads { get; private set; }

    public const string FolderMimeType = "application/vnd.google-apps.folder";

    public void AddFolder(string parentId, string id, string name) =>
        Children(parentId).Add(new DriveEntry(id, name, FolderMimeType, null, null, null));

    public void AddFile(string parentId, string id, string name, byte[] content, DateTime modified)
    {
        Children(parentId).Add(new DriveEntry(
            id, name,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            modified,
            Md5(content),
            content.LongLength));
        _content[id] = content;
        Children(id);
    }

    public void SetFile(string parentId, string id, string name, byte[] content, DateTime modified)
    {
        Children(parentId).RemoveAll(e => e.Id == id);
        AddFile(parentId, id, name, content, modified);
    }

    public void Remove(string parentId, string id) => Children(parentId).RemoveAll(e => e.Id == id);

    public List<DriveEntry> Children(string folderId)
    {
        if (!_children.TryGetValue(folderId, out var list))
        {
            list = [];
            _children[folderId] = list;
        }
        return list;
    }

    public Task<IReadOnlyList<DriveEntry>> ListChildrenAsync(string folderId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DriveEntry>>(Children(folderId).ToList());

    public Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
    {
        Downloads++;
        return Task.FromResult<Stream>(new MemoryStream(_content[fileId], writable: false));
    }

    public static string Md5(byte[] bytes) =>
        Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes)).ToLowerInvariant();
}
