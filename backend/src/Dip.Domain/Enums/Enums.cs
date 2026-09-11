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
