using System.IO;

namespace Dip.Infrastructure.Tests;

// Resolves the samples/ directory relative to the repo root, no matter where
// the test binary lives. Used by importer tests to read the real xlsx files.
internal static class SampleFiles
{
    public static string RepoRoot { get; } = Resolve();

    public static string Path(string relative) =>
        System.IO.Path.Combine(RepoRoot, "samples", relative);

    private static string Resolve()
    {
        // Walk up from the test assembly directory until we find "samples/".
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(System.IO.Path.Combine(dir.FullName, "samples")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Cannot find samples/ directory relative to test binary at " + AppContext.BaseDirectory);
    }
}
