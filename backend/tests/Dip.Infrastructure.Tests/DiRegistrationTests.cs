using Dip.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dip.Infrastructure.Tests;

public class DiRegistrationTests
{
    [Fact]
    public void AddInfrastructure_DoesNotThrow_WithEmptyConfig()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();

        var act = () => services.AddInfrastructure(config);

        act.Should().NotThrow();
    }
}
