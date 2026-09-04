using Dip.Application.Authorization;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class PermissionsSanityTests
{
    [Fact]
    public void Permissions_All_HasUniqueEntries()
    {
        Permissions.All.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void Permissions_All_ContainsCoreEntries()
    {
        Permissions.All.Should().Contain(new[]
        {
            Permissions.ImportRun,
            Permissions.DraftsPromote,
            Permissions.ReportsView,
            Permissions.UsersManage,
        });
    }
}
