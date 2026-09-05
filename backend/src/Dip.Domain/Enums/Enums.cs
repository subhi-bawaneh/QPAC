namespace Dip.Domain.Enums;

public enum DataTarget
{
    Live = 0,
    Draft = 1,
}

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

public enum FileKind
{
    Unknown = 0,
    Tidp = 1,
    Midp = 2,
    Baseline = 3,
    AconexHistory = 4,
    Lists = 5,
    Picklists = 6,
}

public enum FileSource
{
    Drive = 0,
    Upload = 1,
}

public enum ImportState
{
    NotImported = 0,
    Imported = 1,
    Outdated = 2,
    Failed = 3,
}

public enum ImportKind
{
    Tidp = 0,
    Midp = 1,
    AconexHistory = 2,
    Baseline = 3,
    Picklists = 4,
    Lists = 5,
}

public enum DraftRowState
{
    New = 0,
    Modified = 1,
    Unchanged = 2,
    Deleted = 3,
    Conflict = 4,
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
}

// How far a baseline package has got, from its documents (PLAN.md § 5.4.3).
public enum PackageStatus
{
    Unused = 0,     // the package has no documents at all
    Pending = 1,    // it has documents, none submitted yet
    Partial = 2,    // some submitted
    Submitted = 3,  // every document submitted
}
