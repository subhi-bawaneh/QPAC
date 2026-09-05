using System.Text.RegularExpressions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Engine;

// Aconex status string -> UnifiedStatus.
//
// Matching is deliberately forgiving: the exported Aconex data says
// "B - Approved with Comments" while Tracker.xlsx!Lists says "...Comment", and
// spacing varies between exports (docs/excel-analysis.md § 6, discrepancy #4).
// So keys are trimmed, whitespace-collapsed and upper-cased before comparison.
public sealed class StatusMappingLookup
{
    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, UnifiedStatus> _map;

    private StatusMappingLookup(IReadOnlyDictionary<string, UnifiedStatus> map) => _map = map;

    public static StatusMappingLookup Create(IEnumerable<StatusMapping> mappings)
    {
        var map = new Dictionary<string, UnifiedStatus>(StringComparer.Ordinal);
        // Current mappings win over legacy ones carrying the same status text.
        foreach (var mapping in mappings.OrderBy(m => m.IsLegacy))
        {
            var key = Normalize(mapping.AconexStatus);
            if (key.Length > 0)
            {
                map.TryAdd(key, mapping.Status);
            }
        }
        return new StatusMappingLookup(map);
    }

    public UnifiedStatus? Find(string? aconexStatus)
    {
        if (string.IsNullOrWhiteSpace(aconexStatus)) return null;
        return _map.TryGetValue(Normalize(aconexStatus), out var status) ? status : null;
    }

    public static string Normalize(string? value) =>
        value is null ? string.Empty : Whitespace.Replace(value, " ").Trim().ToUpperInvariant();
}
