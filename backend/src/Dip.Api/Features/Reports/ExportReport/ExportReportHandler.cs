using System.Globalization;
using Dip.Application.Abstractions;
using Dip.Application.Engine;
using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Api.Features.Reports.ExportReport;

// Turns a report into sheets of plain values and hands them to the exporter, so the
// xlsx shape lives here and the ClosedXML details stay in Infrastructure.
public sealed class ExportReportHandler : IQueryHandler<ExportReportQuery, ExportedReport>
{
    private readonly DipDbContext _db;
    private readonly IReportExporter _exporter;

    public ExportReportHandler(DipDbContext db, IReportExporter exporter)
    {
        _db = db;
        _exporter = exporter;
    }

    public async Task<ExportedReport> Handle(ExportReportQuery query, CancellationToken ct)
    {
        var data = await ReportDataLoader.LoadAsync(_db, query.ProjectId, ct);

        var sheets = query.Kind switch
        {
            ReportKind.Tracker => Tracker(data),
            ReportKind.CorporateSummary => Corporate(data, query.ReportDate),
            ReportKind.BaselineSummary => Baseline(data),
            ReportKind.Evm => Evm(data, query.ReportDate),
            ReportKind.ControlFindings => await FindingsAsync(data, query.ProjectId, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(query), query.Kind, "Unknown report"),
        };

        var stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
        var fileName = $"{data.Project.Code}-{query.Kind}-{stamp}.xlsx";

        return new ExportedReport(fileName, _exporter.ToWorkbook(sheets));
    }

    private static IReadOnlyList<ExportSheet> Tracker(ReportData data)
    {
        var rowsByDocument = data.TrackerRows.ToDictionary(r => r.DocumentId);

        var rows = data.Documents
            .OrderBy(d => d.DocumentNumber, StringComparer.Ordinal)
            .Select(d =>
            {
                var row = rowsByDocument[d.Id];
                return (IReadOnlyList<object?>)new object?[]
                {
                    d.F04DocType, d.CorporateDiscipline, d.DocumentNumber, d.Title,
                    d.DeliveryMilestone, d.ActivityId, d.PackageName, d.F07Building, d.F08BLevel,
                    d.F05Discipline, d.Exchanges.FirstOrDefault(e => e.Number == 1)?.Author,
                    row.SubmissionsCount, row.Revision, row.AconexStatus, row.Status?.ToString(),
                    row.SubmissionDate, row.DateModified, row.Transmittal,
                    row.PlannedStart, row.PlannedFinish, row.ActualStart, row.ActualFinish,
                };
            })
            .ToList();

        return new[]
        {
            new ExportSheet("Tracker", new[]
            {
                "Type", "Discipline", "Document No", "Title", "Delivery Milestone", "Activity ID",
                "Package Name", "Building", "Level", "Trade", "Author",
                "# of Submissions", "Revision", "Aconex Status", "Status",
                "Submission Date", "Date Modified", "Transmittal",
                "Planned Start", "Planned Finish", "Actual Start", "Actual Finish",
            }, rows),
        };
    }

    private static IReadOnlyList<ExportSheet> Corporate(ReportData data, DateTime? reportDate)
    {
        var summary = CorporateSummaryEngine.Compute(
            data.Documents, data.TrackerRows, data.Project, reportDate);

        var headers = new[]
        {
            "Group", "Total Drawings", "Planned", "Submitted", "Approved",
            "Quality Approved", "Rejected", "Under Review", "Withdrawn", "Total Revisions", "Quality",
            "PV Pending", "PV Sub 1", "PV Sub 2", "PV Approved", "Planned %",
            "EV Pending", "EV Sub 1", "EV Sub 2", "EV Approved", "Completed %",
        };

        IReadOnlyList<object?> ToRow(SummaryGroup g) => new object?[]
        {
            g.Name, g.Total, g.Planned, g.Submitted, g.Approved,
            g.QualityApproved, g.Rejected, g.UnderReview, g.Withdrawn, g.TotalRevisions, g.Quality,
            g.PvPending, g.PvSub1, g.PvSub2, g.PvApproved, g.PlannedPercent,
            g.EvPending, g.EvSub1, g.EvSub2, g.EvApproved, g.CompletedPercent,
        };

        var groups = new List<IReadOnlyList<object?>> { ToRow(summary.Total) };
        groups.AddRange(summary.Disciplines.Select(ToRow));
        groups.AddRange(summary.Authors.Select(ToRow));

        var weeks = summary.Weeks
            .Select(w => (IReadOnlyList<object?>)new object?[]
            {
                w.Number, w.From, w.To, w.Planned, w.Submitted, w.Approved,
                w.CumulativePlanned, w.CumulativeSubmitted, w.CumulativeApproved,
            })
            .ToList();

        return new[]
        {
            new ExportSheet("Corporate Summary", headers, groups),
            new ExportSheet("Timeline", new[]
            {
                "Week", "From", "To", "Weekly Planned", "Weekly Submitted", "Weekly Approved",
                "Cumulative Planned", "Cumulative Submitted", "Cumulative Approved",
            }, weeks),
        };
    }

    private static IReadOnlyList<ExportSheet> Baseline(ReportData data)
    {
        var summary = BaselineSummaryEngine.Compute(data.Documents, data.TrackerRows, data.Baseline);

        var disciplineHeaders = new[]
        {
            "Discipline", "Total Drawings", "Submitted", "Approved",
            "C - Revise and Resubmit", "D - Rejected", "Under Review",
        };

        IReadOnlyList<object?> ToRow(BaselineDisciplineRow r) => new object?[]
        {
            r.Name, r.Total, r.Submitted, r.Approved, r.CRevise, r.DRejected, r.UnderReview,
        };

        var disciplines = new List<IReadOnlyList<object?>> { ToRow(summary.Total) };
        disciplines.AddRange(summary.Disciplines.Select(ToRow));

        var packages = summary.Packages
            .Select(p => (IReadOnlyList<object?>)new object?[]
            {
                p.Package, p.ActivityCode, p.Total, p.Submitted, p.Approved,
                p.CRevise, p.DRejected, p.UnderReview, p.Status.ToString(),
            })
            .ToList();

        var statuses = summary.PackageStatuses
            .Select(s => (IReadOnlyList<object?>)new object?[] { s.Status.ToString(), s.Packages, s.Drawings })
            .ToList();
        statuses.Add(new object?[] { "Total", summary.TotalPackages, summary.TotalPackageDrawings });

        return new[]
        {
            new ExportSheet("Disciplines", disciplineHeaders, disciplines),
            new ExportSheet("Packages", new[]
            {
                "Package", "Activity Code", "Total Drawings", "Submitted", "Approved",
                "C - Revise and Resubmit", "D - Rejected", "Under Review", "Status",
            }, packages),
            new ExportSheet("Packages Summary", new[] { "Status", "No of Packages", "No of Drawings" }, statuses),
        };
    }

    private static IReadOnlyList<ExportSheet> Evm(ReportData data, DateTime? reportDate)
    {
        var summary = EvmEngine.Compute(data.Documents, data.TrackerRows, data.Project, reportDate);

        IReadOnlyList<object?> ToRow(EvmRow r) => new object?[]
        {
            r.Name, r.Documents, r.PlannedValue, r.EarnedValue, r.BudgetAtCompletion,
            r.SchedulePerformanceIndex, r.ScheduleVariance, r.PlannedPercent, r.EarnedPercent,
            r.CostPerformanceIndex,
        };

        var rows = new List<IReadOnlyList<object?>> { ToRow(summary.Total) };
        rows.AddRange(summary.Disciplines.Select(ToRow));

        return new[]
        {
            new ExportSheet("EVM", new[]
            {
                "Group", "Documents", "Planned Value", "Earned Value", "Budget at Completion",
                "SPI", "Schedule Variance", "PV %", "EV %", "CPI",
            }, rows),
        };
    }

    private async Task<IReadOnlyList<ExportSheet>> FindingsAsync(
        ReportData data, Guid projectId, CancellationToken ct)
    {
        var revisions = await UnplannedRevisions.LoadAsync(_db, data, projectId, ct);

        var findings = ControlFindingsEngine.Compute(
            data.Documents, data.TrackerRows, revisions, data.Baseline, data.StatusMappings,
            data.Picklists);

        return new[]
        {
            new ExportSheet("Aconex vs MIDP",
                new[] { "Document No", "Revision", "Title", "Aconex Status", "Status", "Date Modified" },
                findings.DeliveredButUnplanned
                    .Select(f => (IReadOnlyList<object?>)new object?[]
                    {
                        f.DocumentNumber, f.Revision, f.Title, f.AconexStatus,
                        f.Status?.ToString(), f.DateModified,
                    })
                    .ToList()),

            new ExportSheet("Unplanned in MIDP",
                new[] { "Type", "Discipline", "Document No", "Title", "Planned Start", "Author" },
                findings.Unplanned
                    .Select(f => (IReadOnlyList<object?>)new object?[]
                    {
                        f.Type, f.Discipline, f.DocumentNumber, f.Title, f.PlannedStart, f.Author,
                    })
                    .ToList()),

            new ExportSheet("Unused Packages",
                new[] { "Package", "Activity Code", "Original Duration", "Finish", "Total Drawings" },
                findings.UnusedPackages
                    .Select(f => (IReadOnlyList<object?>)new object?[]
                    {
                        f.Package, f.ActivityCode, f.OriginalDuration, f.Finish, f.DocumentCount,
                    })
                    .ToList()),

            new ExportSheet("Duplicate Document No",
                new[] { "Type", "Discipline", "Document No", "Title", "Planned Start", "Author", "Count" },
                findings.Duplicates
                    .Select(f => (IReadOnlyList<object?>)new object?[]
                    {
                        f.Type, f.Discipline, f.DocumentNumber, f.Title, f.PlannedStart, f.Author, f.Count,
                    })
                    .ToList()),
        };
    }
}
