using Dip.Application.Abstractions;

namespace Dip.Infrastructure.Persistence;

internal sealed class SqlDialect : ISqlDialect
{
    public SqlDialect(DatabaseProvider provider) => SupportsILike = provider == DatabaseProvider.Postgres;

    public bool SupportsILike { get; }
}
