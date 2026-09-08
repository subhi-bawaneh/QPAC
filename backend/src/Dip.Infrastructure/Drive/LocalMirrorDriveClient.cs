using System.IO;
using System.Security.Cryptography;
using Dip.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Drive;

// Development stand-in for Google Drive: serves a local copy of the Drive tree
// (GoogleDrive:LocalMirrorPath, e.g. the checked-out Qpac_1 folder) through the
// same read-only IDriveClient contract, so the poller, the importers and the UI run
// end to end on a machine without an API key. Ids are paths relative to the mirror
// root; "." is the root itself. Never registered outside Development.
public sealed class LocalMirrorDriveClient : IDriveClient
{
    public const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string SheetMimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly string _root;

    public LocalMirrorDriveClient(IOptions<GoogleDriveOptions> options)
    {
        var path = options.Value.LocalMirrorPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException("GoogleDrive:LocalMirrorPath is not configured");
        }
        _root = Path.GetFullPath(path);
        if (!Directory.Exists(_root))
        {
            throw new DirectoryNotFoundException($"GoogleDrive:LocalMirrorPath '{_root}' does not exist");
        }
    }

    public Task<IReadOnlyList<DriveEntry>> ListChildrenAsync(string folderId, CancellationToken ct)
    {
        var directory = Resolve(folderId);
        var entries = new List<DriveEntry>();

        foreach (var sub in Directory.EnumerateDirectories(directory).OrderBy(d => d, StringComparer.Ordinal))
        {
            entries.Add(new DriveEntry(IdOf(sub), Path.GetFileName(sub), FolderMimeType,
                Directory.GetLastWriteTimeUtc(sub), null, null));
        }

        foreach (var file in Directory.EnumerateFiles(directory).OrderBy(f => f, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(file);
            // Office lock files and desktop.ini are noise the real Drive never lists.
            if (name.StartsWith(".~lock", StringComparison.Ordinal) || name.StartsWith('~')) continue;
            var info = new FileInfo(file);
            entries.Add(new DriveEntry(IdOf(file), name, SheetMimeType,
                info.LastWriteTimeUtc, Md5Of(file), info.Length));
        }

        return Task.FromResult<IReadOnlyList<DriveEntry>>(entries);
    }

    public Task<Stream> DownloadAsync(string fileId, CancellationToken ct)
    {
        var path = Resolve(fileId);
        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16, useAsync: true);
        return Task.FromResult(stream);
    }

    private string Resolve(string id)
    {
        var full = Path.GetFullPath(Path.Combine(_root, id == "." ? string.Empty : id));
        if (!full.StartsWith(_root, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Drive id '{id}' escapes the mirror root");
        }
        return full;
    }

    private string IdOf(string fullPath) =>
        Path.GetRelativePath(_root, fullPath).Replace('\\', '/');

    private static string Md5Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
    }
}
