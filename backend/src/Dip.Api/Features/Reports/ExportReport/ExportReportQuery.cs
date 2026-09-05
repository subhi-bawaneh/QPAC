using Dip.Application.Abstractions;
using Dip.Application.Authorization;

namespace Dip.Api.Features.Reports.ExportReport;

public enum ReportKind
{
    Tracker = 0,
    CorporateSummary = 1,
    BaselineSummary = 2,
    ControlFindings = 3,
    Evm = 4,
}

// Renders one report as an xlsx workbook. Separate permission from viewing, so a
// reader can be allowed to see numbers without taking them off the platform.
[Permission(Permissions.ReportsExport)]
public sealed record ExportReportQuery(
    Guid ProjectId,
    ReportKind Kind,
    DateTime? ReportDate = null) : IQuery<ExportedReport>;

public sealed record ExportedReport(string FileName, byte[] Content)
{
    public const string ContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
