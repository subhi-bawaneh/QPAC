using Dip.Domain.Common;
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public class BaselineActivity : Entity
{
    public Guid ProjectId { get; set; }
    public string ActivityCode { get; set; } = string.Empty;           // "QP.E.ST.GEN.GEN.1000"
    public string Package { get; set; } = string.Empty;                // "QP | QP.E | QP.E.ST | ..."
    public string WbsLevel1 { get; set; } = string.Empty;
    public string WbsLevel2 { get; set; } = string.Empty;
    public string WbsLevel3 { get; set; } = string.Empty;
    public string WbsLevel4 { get; set; } = string.Empty;
    public string WbsLevel5 { get; set; } = string.Empty;
    public string WbsLevel6 { get; set; } = string.Empty;
    public string WbsLevel7 { get; set; } = string.Empty;
    public BaselineActivityType Type { get; set; }
    public int OriginalDuration { get; set; }
    public DateTime Start { get; set; }
    public DateTime Finish { get; set; }

    public Project? Project { get; set; }
}
