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
            // FilesManage is deliberately absent from every role: uploading, replacing
            // and deleting a source file is the super admin's alone, and SuperAdmin
            // holds it through Permissions.All rather than through this map.
            [Admin] =
            [
                Permissions.ProjectSettings,
                Permissions.ImportRun,
                Permissions.DocumentsEdit,
                Permissions.ReportsView,
                Permissions.ReportsExport,
                Permissions.ListsManage,
                Permissions.BaselineManage,
            ],
            [Manager] =
            [
                Permissions.ImportRun,
                Permissions.DocumentsEdit,
                Permissions.ReportsView,
                Permissions.ReportsExport,
            ],
            [Editor] =
            [
                Permissions.DocumentsEdit,
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
