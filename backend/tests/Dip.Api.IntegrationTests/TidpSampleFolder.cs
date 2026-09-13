using System.Net.Http.Headers;
using System.Text.Json;

namespace Dip.Api.IntegrationTests;

// The real folder, checked in at src/Dip.Api/02.TIDPs, walked exactly the way a client
// walks it: every file with its path relative to the root and its LastWriteTimeUtc,
// every folder including the empty ones.
internal static class TidpSampleFolder
{
    public const string RootName = "02.TIDPs";

    private const string Xlsx =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public static string Root { get; } = Locate();

    public sealed record Entry(string RelativePath, DateTime LastModifiedUtc, long SizeBytes, byte[] Content);

    // Every workbook under the root. Ordered so a test that reaches for "the first
    // file" gets the same one on every machine.
    public static IReadOnlyList<Entry> Files(string? root = null)
    {
        var from = root ?? Root;
        return Directory
            .EnumerateFiles(from, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetFileName(path).StartsWith("~$", StringComparison.Ordinal))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path =>
            {
                var info = new FileInfo(path);
                return new Entry(
                    $"{RootName}/{Relative(from, path)}",
                    info.LastWriteTimeUtc,
                    info.Length,
                    File.ReadAllBytes(path));
            })
            .ToList();
    }

    // Folder paths, empty ones included — `01.NAP/ID-Interior Design` holds nothing and
    // is still part of the project's shape.
    public static IReadOnlyList<string> Folders(string? root = null)
    {
        var from = root ?? Root;
        return Directory
            .EnumerateDirectories(from, "*", SearchOption.AllDirectories)
            .Select(path => $"{RootName}/{Relative(from, path)}")
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToList();
    }

    // The multipart body the endpoint documents: a `manifest` field naming each part,
    // and the parts themselves under the field names it gives.
    public static MultipartFormDataContent Multipart(
        IReadOnlyList<Entry> files, IReadOnlyList<string> folders)
    {
        var content = new MultipartFormDataContent();
        var manifest = new
        {
            rootName = RootName,
            folders,
            files = files.Select((file, index) => new
            {
                field = $"f{index}",
                relativePath = file.RelativePath,
                lastModifiedUtc = file.LastModifiedUtc,
                sizeBytes = file.SizeBytes,
            }).ToList(),
        };

        content.Add(new StringContent(JsonSerializer.Serialize(manifest)), "manifest");

        for (var i = 0; i < files.Count; i++)
        {
            var part = new ByteArrayContent(files[i].Content);
            part.Headers.ContentType = new MediaTypeHeaderValue(Xlsx);
            content.Add(part, $"f{i}", Path.GetFileName(files[i].RelativePath));
        }

        return content;
    }

    // A throwaway copy of the folder, so a test can change a file's bytes and its
    // timestamp without touching what is checked into the repository.
    public static string CopyToTemp()
    {
        var destination = Path.Combine(Path.GetTempPath(), "dip-tidp-" + Guid.NewGuid().ToString("N")[..8]);
        foreach (var directory in Directory.EnumerateDirectories(Root, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(destination, Relative(Root, directory)));
        }
        foreach (var file in Directory.EnumerateFiles(Root, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Relative(Root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
            File.SetLastWriteTimeUtc(target, File.GetLastWriteTimeUtc(file));
        }
        return destination;
    }

    private static string Relative(string root, string path) =>
        Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');

    private static string Locate()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Dip.Api", RootName);
            if (Directory.Exists(candidate)) return candidate;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException($"Cannot locate src/Dip.Api/{RootName}");
    }
}
