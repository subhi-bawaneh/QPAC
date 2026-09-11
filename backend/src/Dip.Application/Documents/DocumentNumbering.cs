using System.Globalization;
using System.Text.RegularExpressions;

namespace Dip.Application.Documents;

// The single implementation of the 8-field numbering scheme (PLAN.md § 5.1.1):
//
//   F01-F02-F03-F04-F05-F06-F07-F08A F08B F08C     (no separator between the last three)
//
// Leading zeros are significant (Zone "00", Sequence "0004"), so every field is a
// string and the two fixed-width fields are left-padded here — never by the caller.
//
// The sequence width is NOT universal: the sample MIDP uses three digits for ANL,
// CAL and REP and four for BIM, MOD, SDW, TDP and VMU, and an SDW with a three-digit
// serial exists too. The width therefore comes from DocumentTypeSerials (see
// SerialWidths) and padding never truncates — a longer serial is kept verbatim.
//
// Used by the Excel importers when parsing rows and by the document editor when a
// user changes one of the eight fields.
public static class DocumentNumbering
{
    public const int ZoneLength = 2;

    // Position of each dash-separated segment, used when reporting which field of a
    // row disagrees with the number the sheet carries in column A.
    public static readonly IReadOnlyList<string> SegmentNames =
    [
        "PROJECT", "ORIGINATOR", "CONTRACT", "DOCUMENT TYPE",
        "DISCIPLINE", "ZONE", "BUILDING", "SEQUENCE",
    ];

    public const int SegmentCount = 8;
    private const int ZoneSegmentIndex = 5;

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    public static string NormalizeZone(string? zone) => Pad(zone, ZoneLength);

    /// Pads to the document type's width, and never truncates: a serial longer than
    /// the width is a data point, not an error to be silently cut.
    public static string NormalizeSequence(string? sequence, int width) => Pad(sequence, width);

    public static string Compose(
        string? f01Project, string? f02Originator, string? f03Contract, string? f04DocType,
        string? f05Discipline, string? f06Zone, string? f07Building,
        string? f08aDrawingType, string? f08bLevel, string? f08cSequence, int sequenceWidth)
    {
        var zone = NormalizeZone(f06Zone);
        var sequence = NormalizeSequence(f08cSequence, sequenceWidth);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Trim(f01Project)}-{Trim(f02Originator)}-{Trim(f03Contract)}-{Trim(f04DocType)}-{Trim(f05Discipline)}-{zone}-{Trim(f07Building)}-{Trim(f08aDrawingType)}{Trim(f08bLevel)}{sequence}");
    }

    /// Structural normalisation of a number as written in the sheet's own DOCUMENT
    /// NUMBER column — the value of record on import (docs/excel-analysis.md).
    /// Whitespace out, upper case, and the zone segment padded to two digits, which is
    /// the one difference between the sheet's form ("-FAC-0-BLAD02-") and the form
    /// Aconex holds. Nothing else is touched: a number that is not eight segments long
    /// is returned cleaned but otherwise as written.
    public static string NormalizeNumber(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        var cleaned = WhitespaceRegex.Replace(raw, string.Empty).ToUpperInvariant();
        var segments = cleaned.Split('-');
        if (segments.Length != SegmentCount) return cleaned;

        segments[ZoneSegmentIndex] = NormalizeZone(segments[ZoneSegmentIndex]);
        return string.Join('-', segments);
    }

    /// The first segment in which two numbers of the same shape disagree, named, or
    /// null when they are equal. Numbers of different shapes report as "NUMBER".
    public static (string Field, string Left, string Right)? FirstDifference(string left, string right)
    {
        if (string.Equals(left, right, StringComparison.Ordinal)) return null;

        var leftSegments = left.Split('-');
        var rightSegments = right.Split('-');
        if (leftSegments.Length != SegmentCount || rightSegments.Length != SegmentCount)
        {
            return ("NUMBER", left, right);
        }

        for (var i = 0; i < SegmentCount; i++)
        {
            if (!string.Equals(leftSegments[i], rightSegments[i], StringComparison.Ordinal))
            {
                return (SegmentNames[i], leftSegments[i], rightSegments[i]);
            }
        }

        return null;
    }

    private static string Pad(string? value, int length) => Trim(value).PadLeft(length, '0');

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
}
