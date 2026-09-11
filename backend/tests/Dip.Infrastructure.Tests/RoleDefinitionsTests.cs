using Dip.Application.Authorization;
using Dip.Infrastructure.Seeding;
using FluentAssertions;
using Xunit;

namespace Dip.Infrastructure.Tests;

public class RoleDefinitionsTests
{
    // files.manage is the super admin's alone. Granting it to a named role would hand
    // uploads, replacements and deletions to everyone in that role; SuperAdmin holds
    // it through Permissions.All instead, which is why this map must not mention it.
    [Fact]
    public void FilesManage_IsGrantedToNoNamedRole()
    {
        RoleDefinitions.RolePermissions
            .Where(r => r.Value.Contains(Permissions.FilesManage))
            .Select(r => r.Key)
            .Should().BeEmpty();
    }

    [Fact]
    public void EveryRolePermission_IsAKnownPermission()
    {
        var known = Permissions.All.ToHashSet();
        RoleDefinitions.RolePermissions
            .SelectMany(r => r.Value)
            .Distinct()
            .Should().OnlyContain(p => known.Contains(p));
    }

    [Fact]
    public void NoRoleKeepsADraftOrFolderPermission()
    {
        RoleDefinitions.RolePermissions
            .SelectMany(r => r.Value)
            .Should().NotContain(p => p.StartsWith("drafts.") || p.StartsWith("folders.") || p.StartsWith("drive."));
    }
}
