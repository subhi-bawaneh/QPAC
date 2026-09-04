using Dip.Domain.Enums;

namespace Dip.Application.Files;

// Pure classifier — no I/O. Rules per PLAN.md § 3.2 + docs/excel-analysis.md.
// Callers can override the returned Kind manually on the FolderFile row.
public static class FileKindDetector
{
    public static FileKind Detect(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return FileKind.Unknown;
        }

        var name = fileName.ToUpperInvariant();

        // Ordering matters: check for the most specific tokens first.
        // TIDP: "-TDP-" segment in the file naming scheme.
        if (name.Contains("-TDP-", StringComparison.Ordinal))
        {
            return FileKind.Tidp;
        }
        if (name.Contains("-MDP-", StringComparison.Ordinal))
        {
            return FileKind.Midp;
        }

        // Whole-word matches, case-insensitive because titles vary.
        if (name.Contains("TRACKER", StringComparison.Ordinal))
        {
            // Tracker.xlsx export contains Aconex history; treat as AconexHistory.
            return FileKind.AconexHistory;
        }
        if (name.Contains("BASELINE", StringComparison.Ordinal))
        {
            return FileKind.Baseline;
        }
        if (name.Contains("PICKLIST", StringComparison.Ordinal))
        {
            return FileKind.Picklists;
        }
        if (name.Contains("LIST", StringComparison.Ordinal))
        {
            return FileKind.Lists;
        }
        if (name.Contains("ACONEX", StringComparison.Ordinal))
        {
            return FileKind.AconexHistory;
        }

        return FileKind.Unknown;
    }
}
