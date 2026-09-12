using FluentAssertions;
using Xunit;

namespace Dip.Infrastructure.Tests;

public class TestConnectionGuardTests
{
    [Theory]
    [InlineData("Host=ep-cool-1234.eu-central-1.aws.neon.tech;Database=dip")]
    [InlineData("Host=EP-COOL.AWS.NEON.TECH;Database=dip")]
    [InlineData("Server=db67935.databaseasp.net;Database=db67935;User Id=db67935;Password=x")]
    [InlineData("Server=DB67935.DATABASEASP.NET;Database=db67935")]
    public void Rejects_ProductionHosts(string connectionString)
    {
        var act = () => TestConnectionGuard.Assert(connectionString);
        act.Should().Throw<InvalidOperationException>().WithMessage(TestConnectionGuard.Message);
    }

    [Theory]
    [InlineData("Server=localhost;Database=dip_test;User Id=sa;Password=test;TrustServerCertificate=true")]
    [InlineData(null)]
    public void Allows_EverythingElse(string? connectionString)
    {
        var act = () => TestConnectionGuard.Assert(connectionString);
        act.Should().NotThrow();
    }
}
