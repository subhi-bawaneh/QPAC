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

    // Folders
    public const string FoldersManage = "folders.manage";
    public const string FoldersAssignTarget = "folders.assignTarget";

    // Drive
    public const string DriveSync = "drive.sync";

    // Imports
    public const string ImportRun = "import.run";

    // Drafts
    public const string DraftsEdit = "drafts.edit";
    public const string DraftsPromote = "drafts.promote";

    // Documents (live)
    public const string DocumentsEditLive = "documents.editLive";

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
        FoldersManage, FoldersAssignTarget,
        DriveSync,
        ImportRun,
        DraftsEdit, DraftsPromote,
        DocumentsEditLive,
        ReportsView, ReportsExport,
        ListsManage,
        BaselineManage,
    };
}
