using Dip.Application.Documents;

namespace Dip.Api.Features.Tidps;

// Bound from the `TidpFolders` section. The keyword lists are configuration rather
// than constants so a new unowned folder — "Provisional Sum (Phase 2)", say — is a
// settings change on the server, not a release.
public sealed class TidpFolderOptions
{
    public const string SectionName = "TidpFolders";

    public string[] UnassignedKeywords { get; set; } = ["Unassigned"];
    public string[] ProvisionalSumKeywords { get; set; } = ["Provisional Sum"];
    public string[] FileExtensions { get; set; } = [".xlsx", ".xlsm"];

    // An empty list in configuration means "not configured", never "match nothing":
    // a blanked-out setting would otherwise silently turn every unowned folder into a
    // subcontractor named "Subcontractor - Unassigned".
    public TidpFolderRules ToRules()
    {
        var defaults = TidpFolderRules.Default;
        return new TidpFolderRules
        {
            UnassignedKeywords = Clean(UnassignedKeywords) ?? defaults.UnassignedKeywords,
            ProvisionalSumKeywords = Clean(ProvisionalSumKeywords) ?? defaults.ProvisionalSumKeywords,
            FileExtensions = Clean(FileExtensions) ?? defaults.FileExtensions,
        };
    }

    private static IReadOnlyList<string>? Clean(string[]? values)
    {
        var cleaned = (values ?? [])
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToArray();
        return cleaned.Length > 0 ? cleaned : null;
    }
}
