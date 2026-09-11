namespace Dip.Application.Abstractions;

// The one place the SQL engine leaks into query code. Postgres has ILIKE; SQLite has
// no ILIKE at all, and its LIKE is already case-insensitive for ASCII — so a handler
// that wants a case-insensitive match asks which operator it may use rather than
// hard-coding EF.Functions.ILike. See docs/local-dev.md § "What differs on SQLite".
public interface ISqlDialect
{
    bool SupportsILike { get; }
}
