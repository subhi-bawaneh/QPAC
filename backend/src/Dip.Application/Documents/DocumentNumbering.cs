using System.Globalization;

namespace Dip.Application.Documents;

// The single implementation of the 8-field numbering scheme (PLAN.md § 5.1.1):
//
//   F01-F02-F03-F04-F05-F06-F07-F08A F08B F08C     (no separator between the last three)
//
// Leading zeros are significant (Zone "00", Sequence "0004"), so every field is a
// string and the two fixed-width fields are left-padded here — never by the caller.
// Used by the Excel importers when parsing rows and by the Draft editor when a user
// changes one of the eight fields.
public static class DocumentNumbering
{
    public const int ZoneLength = 2;
    public const int SequenceLength = 4;

    public static string NormalizeZone(string? zone) => Pad(zone, ZoneLength);

    public static string NormalizeSequence(string? sequence) => Pad(sequence, SequenceLength);

    public static string Compose(
        string? f01Project, string? f02Originator, string? f03Contract, string? f04DocType,
        string? f05Discipline, string? f06Zone, string? f07Building,
        string? f08aDrawingType, string? f08bLevel, string? f08cSequence)
    {
        var zone = NormalizeZone(f06Zone);
        var sequence = NormalizeSequence(f08cSequence);
        return string.Create(CultureInfo.InvariantCulture,
            $"{Trim(f01Project)}-{Trim(f02Originator)}-{Trim(f03Contract)}-{Trim(f04DocType)}-{Trim(f05Discipline)}-{zone}-{Trim(f07Building)}-{Trim(f08aDrawingType)}{Trim(f08bLevel)}{sequence}");
    }

    private static string Pad(string? value, int length) => Trim(value).PadLeft(length, '0');

    private static string Trim(string? value) => value?.Trim() ?? string.Empty;
}
