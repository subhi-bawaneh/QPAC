using Dip.Application.Authorization;

namespace Dip.Infrastructure.Seeding;

// Role -> permission map from PLAN.md § 3.1.
// SuperAdmin gets Permissions.All by default and is not listed here.
internal static class RoleDefinitions
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyDictionary<string, string[]> RolePermissions =
        new Dictionary<string, string[]>
        {
            [Admin] =
            [
                Permissions.ProjectSettings,
                Permissions.FoldersManage,
                Permissions.FoldersAssignTarget,
                Permissions.DriveSync,
                Permissions.ImportRun,
                Permissions.DraftsEdit,
                Permissions.DraftsPromote,
                Permissions.DocumentsEditLive,
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.ListsManage,
                Permissions.BaselineManage,
            ],
            [Manager] =
            [
                Permissions.ImportRun,
                Permissions.DraftsEdit,
                Permissions.DraftsPromote,
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.DocumentsEditLive,
            ],
            [Editor] =
            [
                Permissions.DraftsEdit,
                Permissions.ReportsView,
                Permissions.ReportsExport,
            ],
            [Viewer] =
            [
                Permissions.ReportsView,
                Permissions.ReportsExport,
            ],
        };

    public static IEnumerable<string> AllRoles => new[] { SuperAdmin, Admin, Manager, Editor, Viewer };
}
