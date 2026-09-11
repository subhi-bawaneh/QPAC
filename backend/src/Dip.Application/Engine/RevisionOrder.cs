namespace Dip.Application.Engine;

// Ordering for the Revision string. Every value in the sample is numeric ("00".."07"),
// but Aconex also issues "P00" and "A", and "which of these is later" has to be one
// rule in one place rather than whatever string comparison happens to do.
//
//   a revision that parses as an integer ranks above one that does not
//   two numeric revisions compare by value    (07 > 00, and "7" == "07")
//   two non-numeric revisions compare ordinally, case-insensitively
//
// Callers break the remaining tie by DateModified descending, then source order.
public static class RevisionOrder
{
    /// Positive when <paramref name="left"/> is the later revision.
    public static int Compare(string? left, string? right)
    {
        var leftIsNumeric = TryParse(left, out var leftValue);
        var rightIsNumeric = TryParse(right, out var rightValue);

        if (leftIsNumeric && rightIsNumeric) return leftValue.CompareTo(rightValue);
        if (leftIsNumeric) return 1;
        if (rightIsNumeric) return -1;

        return string.Compare(
            left?.Trim() ?? string.Empty,
            right?.Trim() ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParse(string? revision, out int value) =>
        int.TryParse(
            revision?.Trim(),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out value);
}
