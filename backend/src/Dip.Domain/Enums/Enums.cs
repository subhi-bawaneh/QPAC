namespace Dip.Domain.Enums;

public enum UnifiedStatus
{
    Approved = 0,
    Rejected = 1,
    UnderReview = 2,
    Withdrawn = 3,
}

public enum ScheduleMode
{
    Baseline = 0,
    WorkingPlan = 1,
}

public enum BaselineActivityType
{
    Submittal = 0,
    Approval = 1,
}

public enum ImportKind
{
    Tidp = 0,
    AconexHistory = 1,
    Baseline = 2,
    Picklists = 3,
    Lists = 4,
}

public enum PicklistField
{
    Project = 0,
    Originator = 1,
    Contract = 2,
    DocType = 3,
    Discipline = 4,
    Zone = 5,
    Building = 6,
    DrawingType = 7,
    Level = 8,
    AuthoringSoftware = 9,
    ExchangeFormat = 10,
    ScopeArea = 11,
    SuitabilityCode = 12,
    Scale = 13,
    Classification = 14,
    CorporateDiscipline = 15,
    Author = 16,
}

// What a queued unit of background work does. In-memory only (WorkQueue) —
// never persisted, so the worker can be restarted without draining a table.
public enum WorkItemKind
{
    ImportBatch = 0,
    Recalculate = 1,
}

// How far a baseline package has got, from its documents (PLAN.md § 5.4.3).
public enum PackageStatus
{
    Unused = 0,     // the package has no documents at all
    Pending = 1,    // it has documents, none submitted yet
    Partial = 2,    // some submitted
    Submitted = 3,  // every document submitted
}

// How far an uploaded TIDP workbook got. A row that is still Importing when the
// process restarts is swept to Failed, because its bytes are gone with the queue.
public enum TidpFileStatus
{
    Importing = 0,
    Imported = 1,
    Failed = 2,
}

public enum ImportBatchStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
}

// What a level-1 folder under the TIDP root stands for. Read from the folder's own
// name against the keywords in configuration, never from the workbooks inside it:
// `06.Subcontractor - Unassigned` holds files whose header still says "Architectural".
public enum TidpOwnerType
{
    Subcontractor = 0,  // a named trade contractor — NAP, JINGGONG, TKE …
    Unassigned = 1,     // no subcontractor appointed yet; the folder has no owner name
    ProvisionalSum = 2, // work carried as a provisional sum; likewise unowned
}

// Whether the last folder sync still found this row's path on disk. Separate from
// TidpFileStatus, which says how the workbook's *import* went: a file can be
// Imported and Missing at once (someone moved it out of the folder after it loaded).
// A path that disappears is never deleted, only marked — it is usually a move.
public enum TidpFolderStatus
{
    Present = 0,
    Missing = 1,
}

// What one folder sync did with one path. Added/Updated queue an import; Skipped
// touches LastSeenAt and nothing else; Missing is a stored path this upload did not
// carry; Failed never reached the queue (bad name, unreadable workbook).
public enum TidpSyncAction
{
    Added = 0,
    Updated = 1,
    Skipped = 2,
    Missing = 3,
    Failed = 4,
}
