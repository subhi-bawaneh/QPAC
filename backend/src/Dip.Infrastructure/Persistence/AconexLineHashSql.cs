namespace Dip.Infrastructure.Persistence;

// AconexLineHasher, expressed in SQL.
//
// The S3 migration has to hash rows that are already in the table, and pulling 25k of
// them through C# inside a migration is not something a migration should do. That makes
// this a second implementation of the same function — and a second implementation that
// drifts would silently re-insert the entire history on the next upload, because every
// stored hash would stop matching.
//
// So it lives in one place, the migration uses it, and
// AconexAppendTests.TheMigrationsSqlBackfill_ProducesTheSameHashAsTheHasher pins the two
// together against the real export. Postgres only: the SQLite dev database is built by
// EnsureCreated and never runs a migration.
internal static class AconexLineHashSql
{
    // Every character .NET's char.IsWhiteSpace accepts. POSIX \s stops at ASCII, and
    // the real export carries 330 non-breaking spaces across 216 rows — without these
    // the two implementations disagree on exactly those rows.
    private const string WhitespaceClass =
        @"[\s\u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]+";

    private const string DateFormat = @"'YYYY-MM-DD""T""HH24:MI:SS.US'";

    private static readonly string[] TextColumnsBeforeDate =
    [
        "FileType", "FileName", "AconexDocNo", "Revision", "Title", "AconexStatus", "ReviewStatus",
    ];

    private static readonly string[] TextColumnsAfterDate =
    [
        "Type", "Discipline", "Area", "Venue", "FloorLevel", "TransmittalIn",
    ];

    /// The hex SHA-256 of one row, as a SQL expression over the named table alias.
    public static string HashExpression(string tableAlias)
    {
        var fields = TextColumnsBeforeDate.Select(c => Normalized(tableAlias, c))
            .Append($"to_char({tableAlias}.\"DateModified\", {DateFormat})")
            .Concat(TextColumnsAfterDate.Select(c => Normalized(tableAlias, c)));

        // The unit separator (chr(31)) trails every field, exactly as the hasher writes
        // it: it cannot occur inside an Aconex cell, so two different lines can never
        // join into one string.
        var joined = string.Join(" || chr(31) || ", fields) + " || chr(31)";
        return $"encode(sha256(convert_to({joined}, 'UTF8')), 'hex')";
    }

    // Trim, collapse every run of whitespace to one space, upper-case.
    private static string Normalized(string alias, string column) =>
        $"btrim(regexp_replace(upper(coalesce({alias}.\"{column}\", '')), '{WhitespaceClass}', ' ', 'g'))";
}
