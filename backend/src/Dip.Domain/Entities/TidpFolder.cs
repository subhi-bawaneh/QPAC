using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

// The TIDP root's own shape, recorded so a second upload can be compared against the
// first rather than reloaded wholesale. Both folder levels are rows because both carry
// meaning the workbooks do not: the owner folder says whose scope a file is, and a
// discipline folder is worth keeping even when it holds nothing — `01.NAP/ID-Interior
// Design` being empty is a fact about the project, not an absence of one.

// A level-1 folder: `01.NAP`, `06.Subcontractor - Unassigned`, `08. Provisional Sum`.
public class TidpFolderOwner : AuditableEntity
{
    public Guid ProjectId { get; set; }

    // Key. The folder's path relative to the root, normalised to forward slashes and
    // compared case-insensitively — for a level-1 folder, its own name.
    public string RelativePath { get; set; } = string.Empty;
    public string FolderName { get; set; } = string.Empty;      // raw, `07. RAWABI`

    public int? SortOrder { get; set; }                          // 7
    public string? OwnerName { get; set; }                       // "RAWABI"; null when unowned
    public TidpOwnerType OwnerType { get; set; } = TidpOwnerType.Subcontractor;

    public TidpFolderStatus FolderStatus { get; set; } = TidpFolderStatus.Present;
    public DateTime LastSeenAt { get; set; }
    public DateTime? MissingSince { get; set; }

    public Project? Project { get; set; }
    public ICollection<TidpFolderDiscipline> Disciplines { get; set; } = new List<TidpFolderDiscipline>();
}

// A level-2 folder: `AR-Architectural`, `FY-Fire & Life Safety`. Its two-letter code is
// the folder's own namespace and is deliberately not Discipline.Code, which is the
// three-letter corporate one (AR here, ARC there).
public class TidpFolderDiscipline : AuditableEntity
{
    public Guid ProjectId { get; set; }
    public Guid OwnerId { get; set; }

    public string RelativePath { get; set; } = string.Empty;     // `01.NAP/AR-Architectural`
    public string FolderName { get; set; } = string.Empty;       // `AR-Architectural`
    public string DisciplineCode { get; set; } = string.Empty;   // "AR"
    public string DisciplineName { get; set; } = string.Empty;   // "Architectural"

    public TidpFolderStatus FolderStatus { get; set; } = TidpFolderStatus.Present;
    public DateTime LastSeenAt { get; set; }
    public DateTime? MissingSince { get; set; }

    public Project? Project { get; set; }
    public TidpFolderOwner? Owner { get; set; }
}

// One run of the folder sync. The per-file plan is held as JSON rather than as a child
// table: it is read back whole, by one screen, once — and the authoritative state of
// every file is on TidpFile anyway, which is what the read side overlays onto this.
public class TidpFolderSync : Entity
{
    public Guid ProjectId { get; set; }
    public string RootName { get; set; } = string.Empty;         // "02.TIDPs"
    public DateTime StartedAt { get; set; }
    public string StartedBy { get; set; } = string.Empty;

    public int TotalFiles { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public int Missing { get; set; }
    public int Failed { get; set; }

    public string? ResultJson { get; set; }

    public Project? Project { get; set; }
}
