using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Dip.Application.Documents;

// The identity of one Aconex event line.
//
// The owner exports from Aconex periodically and cannot remember what was already
// loaded, so an upload keeps only what is new and a line counts as already held when
// every one of its source columns matches. That is a hash of the whole line, not of
// (document, revision, date): Aconex can re-export the same event with a corrected
// title, and those two rows are genuinely different lines.
//
// Two rules make the hash stable across exports:
//
//   - Only the fourteen SOURCE columns go in. DocNoFinal, the computed flags, the
//     batch id and the row id are all excluded, so changing a cleaning rule does not
//     make every previously loaded line look new.
//   - Values are trimmed, internal whitespace is collapsed, text is upper-cased and
//     the date is written to fixed microsecond precision. Without that, one export's
//     trailing space or "14:56:39.027" against "14:56:39.0270000" would hash
//     differently and the same event would be inserted twice.
public static class AconexLineHasher
{
    // ASCII unit separator: chosen because it cannot appear inside an Aconex cell.
    // Joining on a character that can would let two different lines produce one string.
    private const char Separator = '\u001F';

    private const string DateFormat = "yyyy-MM-ddTHH:mm:ss.ffffff";

    public static string Compute(
        string? fileType,
        string? fileName,
        string? aconexDocNo,
        string? revision,
        string? title,
        string? aconexStatus,
        string? reviewStatus,
        DateTime dateModified,
        string? type,
        string? discipline,
        string? area,
        string? venue,
        string? floorLevel,
        string? transmittalIn)
    {
        var builder = new StringBuilder(512);
        Append(builder, fileType);
        Append(builder, fileName);
        Append(builder, aconexDocNo);
        Append(builder, revision);
        Append(builder, title);
        Append(builder, aconexStatus);
        Append(builder, reviewStatus);
        Append(builder, dateModified.ToString(DateFormat, CultureInfo.InvariantCulture));
        Append(builder, type);
        Append(builder, discipline);
        Append(builder, area);
        Append(builder, venue);
        Append(builder, floorLevel);
        Append(builder, transmittalIn);

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string? value)
    {
        builder.Append(Normalize(value));
        builder.Append(Separator);
    }

    // Collapses every run of whitespace to one space, trims, and upper-cases.
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                if (builder.Length > 0) pendingSpace = true;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(char.ToUpperInvariant(ch));
        }

        return builder.ToString();
    }
}
