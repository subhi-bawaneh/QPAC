using System.Text.RegularExpressions;
using Dip.Domain.Enums;

namespace Dip.Application.Documents;

// Everything the TIDP folder's own shape says about a file, derived from strings
// alone: no file system, no workbook, no database. The folder is the source of
// truth for owner and discipline because the workbooks are not — a file sitting in
// `06.Subcontractor - Unassigned` still carries "Architectural" in its DISCIPLINE
// header and the placeholder `...-TDP-XXX-...` in its DOCUMENT REFERENCE.
//
// The unit of identity is the path relative to the root, never the file name: the
// sample holds `...-TDP-ARC-00-000000-000001.xlsx` three times over, under
// `01.NAP/AR-Architectural`, `08. Provisional Sum` and `11. NAP PMO/AR-Architectural`.
public sealed class TidpPathParser
{
    // `01.NAP`, `07. RAWABI`, `12. SANA AL-JAZERAH` — a number, a dot, an optional space.
    private static readonly Regex OwnerFolder = new(
        @"^\s*(?<order>\d+)\s*\.\s*(?<name>.*)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // `AR-Architectural`, `FY-Fire & Life Safety` — two letters, a dash, the rest.
    // Only the first dash splits: "Fire & Life Safety" keeps its own punctuation.
    private static readonly Regex DisciplineFolder = new(
        @"^\s*(?<code>[A-Za-z]{2})\s*-\s*(?<name>.+)$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly TidpFolderRules _rules;

    public TidpPathParser(TidpFolderRules? rules = null) => _rules = rules ?? TidpFolderRules.Default;

    public TidpFolderRules Rules => _rules;

    // ------------------------------------------------------------------ paths

    // The comparison key. Backslashes become slashes so a Windows client and a browser
    // agree, empty and "." segments go, and the result is compared case-insensitively
    // everywhere — Windows would not distinguish `01.NAP` from `01.nap` either.
    public static string Normalize(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return string.Empty;

        var segments = relativePath
            .Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Trim())
            .Where(segment => segment.Length > 0 && segment != ".");

        return string.Join('/', segments);
    }

    // The root folder's own name is not part of the key: the operator may upload
    // `02.TIDPs`, `TIDPs` or a renamed copy, and the same file must still match the
    // row it made last time. A manifest that repeats the root prefix on every entry
    // has it stripped here rather than in each caller.
    public static string StripRoot(string? relativePath, string? rootName)
    {
        var path = Normalize(relativePath);
        if (string.IsNullOrEmpty(rootName)) return path;

        var prefix = Normalize(rootName) + "/";
        return path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? path[prefix.Length..]
            : path;
    }

    // The greatest common first segment of every path, when there is one and it is a
    // folder rather than a file sitting at the top. That is the root the operator
    // picked, and it is what gets stripped.
    public static string? DetectRoot(IEnumerable<string> relativePaths)
    {
        string? candidate = null;
        var any = false;

        foreach (var path in relativePaths.Select(Normalize).Where(p => p.Length > 0))
        {
            var slash = path.IndexOf('/');
            // A file at the top level means there is no single wrapping folder.
            if (slash < 0) return null;

            var first = path[..slash];
            if (!any)
            {
                candidate = first;
                any = true;
            }
            else if (!string.Equals(candidate, first, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        return candidate;
    }

    // ---------------------------------------------------------------- folders

    // A level-1 folder. A name without the `NN.` prefix is still an owner — it is the
    // operator's folder, not ours to reject — but it sorts last and says so.
    public ParsedOwnerFolder ParseOwnerFolder(string folderName)
    {
        var raw = (folderName ?? string.Empty).Trim();
        var warnings = new List<string>();

        int? sortOrder = null;
        var name = raw;

        var match = OwnerFolder.Match(raw);
        if (match.Success)
        {
            if (int.TryParse(match.Groups["order"].Value, out var parsed)) sortOrder = parsed;
            name = match.Groups["name"].Value.Trim();
        }
        else
        {
            warnings.Add($"Owner folder '{raw}' has no 'NN.' prefix - it will sort last");
        }

        var ownerType = ClassifyOwner(name);

        return new ParsedOwnerFolder(
            FolderName: raw,
            SortOrder: sortOrder,
            // Unassigned and provisional-sum folders have no subcontractor yet, and
            // storing "Subcontractor - Unassigned" as an owner name would invent one.
            OwnerName: ownerType == TidpOwnerType.Subcontractor ? NullIfBlank(name) : null,
            OwnerType: ownerType,
            Warnings: warnings);
    }

    private TidpOwnerType ClassifyOwner(string name)
    {
        if (Matches(name, _rules.UnassignedKeywords)) return TidpOwnerType.Unassigned;
        if (Matches(name, _rules.ProvisionalSumKeywords)) return TidpOwnerType.ProvisionalSum;
        return TidpOwnerType.Subcontractor;
    }

    private static bool Matches(string name, IReadOnlyList<string> keywords) =>
        keywords.Any(keyword =>
            !string.IsNullOrWhiteSpace(keyword)
            && name.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase));

    // A level-2 folder. Null when the name is not `XX-Name`: the caller keeps the file
    // and records the folder in its relative path rather than losing either.
    public ParsedDisciplineFolder? ParseDisciplineFolder(string folderName)
    {
        var raw = (folderName ?? string.Empty).Trim();
        var match = DisciplineFolder.Match(raw);
        if (!match.Success) return null;

        return new ParsedDisciplineFolder(
            FolderName: raw,
            DisciplineCode: match.Groups["code"].Value.Trim().ToUpperInvariant(),
            DisciplineName: match.Groups["name"].Value.Trim());
    }

    // ------------------------------------------------------------------ files

    // Why a file is not a TIDP workbook, or null when it is one. Lock files and hidden
    // files are the normal contents of a real folder, not defects — they are reported
    // as skipped rather than failed.
    public string? SkipReason(string fileName)
    {
        var name = (fileName ?? string.Empty).Trim();

        if (name.Length == 0) return "empty file name";
        if (name.StartsWith("~$", StringComparison.Ordinal)) return "Excel lock file";
        if (name.StartsWith('.')) return "hidden file";

        var extension = Path.GetExtension(name);
        if (!_rules.FileExtensions.Any(allowed =>
                string.Equals(allowed, extension, StringComparison.OrdinalIgnoreCase)))
        {
            return extension.Length == 0
                ? "no file extension"
                : $"'{extension}' is not a TIDP workbook extension";
        }

        return null;
    }

    // `QF01012-NES-C04518-TDP-ARC-00-000000-000001.xlsx` -> its eight fields.
    // Sequence stays a string and its length is never assumed: the sample carries
    // `000001`, `900001` and RAWABI's five-digit `19000`, and `00`/`000000` would lose
    // their leading zeros the moment either became a number (PLAN.md § 1).
    public static ParsedTidpFileName? ParseFileName(string fileName, out string? error)
    {
        error = null;
        var stem = Path.GetFileNameWithoutExtension((fileName ?? string.Empty).Trim());
        if (stem.Length == 0)
        {
            error = "empty file name";
            return null;
        }

        var tokens = stem.Split('-');
        if (tokens.Length != 8)
        {
            error =
                $"'{stem}' has {tokens.Length} dash-separated part(s), expected 8 "
                + "(PROJECT-ORIGINATOR-CONTRACT-DOCTYPE-DISCIPLINE-ZONE-LEVEL-SEQUENCE)";
            return null;
        }

        var trimmed = tokens.Select(t => t.Trim()).ToArray();
        if (trimmed.Any(t => t.Length == 0))
        {
            error = $"'{stem}' has an empty part";
            return null;
        }

        var sequence = trimmed[7];
        if (!sequence.All(char.IsAsciiDigit))
        {
            error = $"'{stem}' ends in '{sequence}', which is not a sequence number";
            return null;
        }

        var warnings = new List<string>();
        if (trimmed[4].Length != 3 || !trimmed[4].All(char.IsAsciiLetter))
        {
            // A warning, not a rejection: the tag is what the folder rules key on, and
            // an odd one is worth seeing rather than worth dropping the file over.
            warnings.Add($"discipline tag '{trimmed[4]}' is not three letters");
        }

        return new ParsedTidpFileName(
            ProjectCode: trimmed[0],
            Originator: trimmed[1],
            Contract: trimmed[2],
            DocType: trimmed[3],
            DisciplineTag: trimmed[4].ToUpperInvariant(),
            Zone: trimmed[5],
            Level: trimmed[6],
            Sequence: sequence,
            Warnings: warnings);
    }

    // ------------------------------------------------------------- whole path

    // One file's path, relative to the root, resolved into everything the sync needs.
    // Never throws and never returns null: a path that cannot be imported comes back
    // carrying its reason, because one bad name must not end the upload (§ 8).
    public ParsedTidpPath ParsePath(string relativePath)
    {
        var path = Normalize(relativePath);
        var warnings = new List<string>();

        if (path.Length == 0)
        {
            return ParsedTidpPath.Rejected(path, string.Empty, "empty path");
        }

        var segments = path.Split('/');
        var fileName = segments[^1];
        var folders = segments[..^1];

        if (folders.Length == 0)
        {
            // Loose at the root: no owner to attribute it to, so there is nothing
            // sensible to import it as.
            return ParsedTidpPath.Rejected(path, fileName, "file sits at the root, outside any owner folder");
        }

        var skip = SkipReason(fileName);
        if (skip is not null)
        {
            return ParsedTidpPath.Skipped(path, fileName, skip);
        }

        var owner = ParseOwnerFolder(folders[0]);
        warnings.AddRange(owner.Warnings);

        ParsedDisciplineFolder? discipline = null;
        if (folders.Length >= 2)
        {
            discipline = ParseDisciplineFolder(folders[1]);
            if (discipline is null)
            {
                warnings.Add(
                    $"'{folders[1]}' is not a 'XX-Name' discipline folder - "
                    + "the file is kept under its full path with no discipline folder");
            }
        }

        // Two levels is the shape; more is not a reason to lose a file. It is imported
        // under its full relative path, which is the key anyway, and said out loud.
        if (folders.Length > 2)
        {
            warnings.Add(
                $"nested {folders.Length} folder(s) deep, deeper than owner/discipline - "
                + "imported under its full relative path");
        }

        var parsedName = ParseFileName(fileName, out var error);
        if (parsedName is null)
        {
            return new ParsedTidpPath(
                path, fileName, owner, discipline, null, warnings, error, IsSkipped: false);
        }

        warnings.AddRange(parsedName.Warnings);

        return new ParsedTidpPath(
            path, fileName, owner, discipline, parsedName, warnings, Error: null, IsSkipped: false);
    }

    // The path of the owner folder, and of the discipline folder inside it — the keys
    // those two rows are upserted by, derived the same way the file's own key is.
    public static string OwnerPath(string normalizedFilePath) =>
        normalizedFilePath.Split('/') is { Length: > 1 } segments ? segments[0] : string.Empty;

    public static string? DisciplinePath(string normalizedFilePath)
    {
        var segments = normalizedFilePath.Split('/');
        return segments.Length > 2 ? $"{segments[0]}/{segments[1]}" : null;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

// The keyword lists that decide what an owner folder means, and which extensions are
// workbooks. Data rather than code so `appsettings.json` can extend them — a new
// "Provisional Sum (Phase 2)" folder must not need a deployment.
public sealed record TidpFolderRules
{
    public IReadOnlyList<string> UnassignedKeywords { get; init; } = ["Unassigned"];
    public IReadOnlyList<string> ProvisionalSumKeywords { get; init; } = ["Provisional Sum"];
    public IReadOnlyList<string> FileExtensions { get; init; } = [".xlsx", ".xlsm"];

    public static TidpFolderRules Default { get; } = new();
}

public sealed record ParsedOwnerFolder(
    string FolderName,
    int? SortOrder,
    string? OwnerName,
    TidpOwnerType OwnerType,
    IReadOnlyList<string> Warnings);

public sealed record ParsedDisciplineFolder(
    string FolderName,
    string DisciplineCode,
    string DisciplineName);

public sealed record ParsedTidpFileName(
    string ProjectCode,
    string Originator,
    string Contract,
    string DocType,
    string DisciplineTag,
    string Zone,
    string Level,
    string Sequence,
    IReadOnlyList<string> Warnings);

// One path's verdict. Exactly one of three states: importable (Error null, not
// skipped), skipped (something that was never a TIDP file), or failed (a TIDP file
// whose name does not parse).
public sealed record ParsedTidpPath(
    string RelativePath,
    string FileName,
    ParsedOwnerFolder? Owner,
    ParsedDisciplineFolder? Discipline,
    ParsedTidpFileName? File,
    IReadOnlyList<string> Warnings,
    string? Error,
    bool IsSkipped)
{
    public bool IsImportable => Error is null && !IsSkipped && File is not null;

    internal static ParsedTidpPath Rejected(string path, string fileName, string error) =>
        new(path, fileName, null, null, null, [], error, IsSkipped: false);

    internal static ParsedTidpPath Skipped(string path, string fileName, string reason) =>
        new(path, fileName, null, null, null, [], reason, IsSkipped: true);
}
