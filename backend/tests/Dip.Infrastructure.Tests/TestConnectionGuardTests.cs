using FluentAssertions;
using Xunit;

namespace Dip.Infrastructure.Tests;

public class TestConnectionGuardTests
{
    [Theory]
    [InlineData("Host=ep-cool-1234.eu-central-1.aws.neon.tech;Database=dip")]
    [InlineData("Host=EP-COOL.AWS.NEON.TECH;Database=dip")]
    public void Rejects_NeonHosts(string connectionString)
    {
        var act = () => TestConnectionGuard.Assert(connectionString);
        act.Should().Throw<InvalidOperationException>().WithMessage(TestConnectionGuard.Message);
    }

    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=dip_test;Username=postgres;Password=postgres")]
    [InlineData(null)]
    public void Allows_EverythingElse(string? connectionString)
    {
        var act = () => TestConnectionGuard.Assert(connectionString);
        act.Should().NotThrow();
    }
}
