namespace Dip.Application.Authorization;

// All permission strings referenced in PLAN.md § 3.1.
// Kept as string constants so they can be used in attributes and role seeds.
public static class Permissions
{
    // System-wide
    public const string UsersManage = "users.manage";
    public const string RolesManage = "roles.manage";
    public const string SystemSettings = "system.settings";

    // Project
    public const string ProjectSettings = "project.settings";

    // Imports
    public const string ImportRun = "import.run";

    // Upload, replace and delete a source file. Granted to no role in RoleDefinitions:
    // SuperAdmin holds it through Permissions.All, and nobody else has it.
    public const string FilesManage = "files.manage";

    // Documents (live)
    public const string DocumentsEdit = "documents.edit";

    // Reports
    public const string ReportsView = "reports.view";
    public const string ReportsExport = "reports.export";

    // Lists / Picklists / StatusMapping
    public const string ListsManage = "lists.manage";

    // Baseline
    public const string BaselineManage = "baseline.manage";

    public static IReadOnlyCollection<string> All { get; } = new[]
    {
        UsersManage, RolesManage, SystemSettings,
        ProjectSettings,
        ImportRun, FilesManage,
        DocumentsEdit,
        ReportsView, ReportsExport,
        ListsManage,
        BaselineManage,
    };
}
