using Testcontainers.PostgreSql;
using Xunit;

namespace Dip.Infrastructure.Tests;

// One Postgres container per test class. Reuses the container across [Fact]s
// via IAsyncLifetime, and lets docker clean it up on dispose.
public sealed class PostgresFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("dip_test")
        .WithUsername("dip")
        .WithPassword("dip_test_password")
        .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
