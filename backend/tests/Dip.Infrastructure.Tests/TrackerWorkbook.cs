using Dip.Application.Abstractions;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Infrastructure.Excel;
using Dip.Infrastructure.Seeding;

namespace Dip.Infrastructure.Tests;

// Reads samples/Tracker.xlsx: the engine inputs (documents, Aconex revisions, baseline
// activities) and the cached outputs the engines must reproduce.
//
// Inputs are taken from the columns the workbook's own formulas read — in particular
// `SHD_History!Document No Final` and `Terminated`, which every Tracker MAXIFS/MINIFS
// matches on. That column carries no formula and is not always derivable from its row
// (docs/excel-analysis.md § 6, finding 18), so it is an input here rather than
// something to recompute; the importer's normalisation is covered by NormalizeDocNoTests.
internal static class TrackerWorkbook
{
    public static IExcelWorkbook Open() =>
        new ClosedXmlReader().Open(SampleFiles.Path("Tracker.xlsx"));

    // ------------------------------------------------------------------ inputs

    public sealed record TrackerSheetRow(
        string DocumentNumber,
        string? Discipline,
        string? Author,
        DateTime? DeliveryMilestone,
        string? ActivityId,
        int? SubmissionsCount,
        string? Revision,
        string? AconexStatus,
        string? Status,
        DateTime? SubmissionDate,
        DateTime? DateModified,
        string? Transmittal,
        DateTime? PlannedStart,
        DateTime? PlannedFinish,
        DateTime? ActualStart,
        DateTime? ActualFinish);

    public static IReadOnlyList<TrackerSheetRow> ReadTrackerSheet(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Tracker");
        var headerRow = HeaderRow(sheet, "Document No");

        var colDocumentNo = Column(sheet, headerRow, "Document No");
        var colDiscipline = Column(sheet, headerRow, "Discipline");
        var colAuthor = Column(sheet, headerRow, "Author");
        var colMilestone = Column(sheet, headerRow, "Delivery Milestone");
        var colActivityId = Column(sheet, headerRow, "Activity ID");
        var colSubmissions = Column(sheet, headerRow, "# of Submissions");
        var colRevision = Column(sheet, headerRow, "Revision");
        var colAconexStatus = Column(sheet, headerRow, "Aconex Status");
        var colStatus = Column(sheet, headerRow, "Status");
        var colSubmissionDate = Column(sheet, headerRow, "Submission Date");
        var colDateModified = Column(sheet, headerRow, "Date Modified");
        var colTransmittal = Column(sheet, headerRow, "Transmittal");
        var colPlannedStart = Column(sheet, headerRow, "Planned Start");
        var colPlannedFinish = Column(sheet, headerRow, "Planned Finish");
        var colActualStart = Column(sheet, headerRow, "Actual Start");
        var colActualFinish = Column(sheet, headerRow, "Actual Finish");

        var rows = new List<TrackerSheetRow>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var documentNumber = row.Cell(colDocumentNo).GetStringOrNull()?.Trim();
            if (string.IsNullOrEmpty(documentNumber)) continue;

            rows.Add(new TrackerSheetRow(
                DocumentNumber: documentNumber,
                Discipline: Text(row.Cell(colDiscipline)),
                Author: Text(row.Cell(colAuthor)),
                DeliveryMilestone: row.Cell(colMilestone).GetDateTime(),
                ActivityId: Text(row.Cell(colActivityId)),
                SubmissionsCount: row.Cell(colSubmissions).GetInt(),
                Revision: Text(row.Cell(colRevision)),
                AconexStatus: Text(row.Cell(colAconexStatus)),
                Status: Text(row.Cell(colStatus)),
                SubmissionDate: row.Cell(colSubmissionDate).GetDateTime(),
                DateModified: row.Cell(colDateModified).GetDateTime(),
                Transmittal: Text(row.Cell(colTransmittal)),
                PlannedStart: row.Cell(colPlannedStart).GetDateTime(),
                PlannedFinish: row.Cell(colPlannedFinish).GetDateTime(),
                ActualStart: row.Cell(colActualStart).GetDateTime(),
                ActualFinish: row.Cell(colActualFinish).GetDateTime()));
        }
        return rows;
    }

    // The Corporate Summary groups by CorporateDiscipline and by the Exchange 01 author.
    public static Document ToDocument(TrackerSheetRow row) => new()
    {
        Id = Guid.NewGuid(),
        DocumentNumber = row.DocumentNumber,
        ActivityId = row.ActivityId,
        DeliveryMilestone = row.DeliveryMilestone,
        CorporateDiscipline = row.Discipline ?? string.Empty,
        Exchanges = new List<DataExchange> { new() { Number = 1, Author = row.Author } },
    };

    public static IReadOnlyList<AconexRevision> ReadAconexHistory(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("SHD_History");
        var headerRow = HeaderRow(sheet, "Document No Final");

        var colDocNo = Column(sheet, headerRow, "Document No");
        var colDocNoFinal = Column(sheet, headerRow, "Document No Final");
        var colRevision = Column(sheet, headerRow, "Revision");
        var colStatus = Column(sheet, headerRow, "Status");
        var colReviewStatus = Column(sheet, headerRow, "Review Status");
        var colDateModified = Column(sheet, headerRow, "Date Modified");
        var colTransmittal = Column(sheet, headerRow, "Transmittal In");
        var colTerminated = Column(sheet, headerRow, "Terminated");

        var rows = new List<AconexRevision>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var dateModified = row.Cell(colDateModified).GetDateTime();
            if (dateModified is null) continue;

            rows.Add(new AconexRevision
            {
                AconexDocNo = Text(row.Cell(colDocNo)) ?? string.Empty,
                DocNoFinal = Text(row.Cell(colDocNoFinal)) ?? string.Empty,
                Revision = Text(row.Cell(colRevision)) ?? string.Empty,
                AconexStatus = Text(row.Cell(colStatus)) ?? string.Empty,
                ReviewStatus = Text(row.Cell(colReviewStatus)),
                DateModified = dateModified.Value,
                TransmittalIn = Text(row.Cell(colTransmittal)),
                IsTerminated = string.Equals(
                    Text(row.Cell(colTerminated)), "TRUE", StringComparison.OrdinalIgnoreCase),
            });
        }
        return rows;
    }

    public static IReadOnlyList<BaselineActivity> ReadBaseline(IExcelWorkbook workbook)
    {
        var sheet = workbook.Sheet("Baseline");
        var headerRow = HeaderRow(sheet, "Activity Code");

        var colActivityCode = Column(sheet, headerRow, "Activity Code");
        var colActivity = Column(sheet, headerRow, "Activity");
        var colPackage = Column(sheet, headerRow, "Package");
        var colStart = Column(sheet, headerRow, "Start");
        var colFinish = Column(sheet, headerRow, "Finish");

        var rows = new List<BaselineActivity>();
        for (var r = headerRow + 1; r <= sheet.RowCount; r++)
        {
            var row = sheet.Row(r);
            var code = Text(row.Cell(colActivityCode));
            if (string.IsNullOrEmpty(code)) continue;

            // Only the two known activity types produce a baseline row — 12 rows in the
            // sample leave the Activity column blank, and BaselineImporter skips those
            // too rather than guessing.
            var activity = Text(row.Cell(colActivity));
            BaselineActivityType type;
            if (string.Equals(activity, "Submittal", StringComparison.OrdinalIgnoreCase))
            {
                type = BaselineActivityType.Submittal;
            }
            else if (string.Equals(activity, "Approval", StringComparison.OrdinalIgnoreCase))
            {
                type = BaselineActivityType.Approval;
            }
            else
            {
                continue;
            }

            rows.Add(new BaselineActivity
            {
                ActivityCode = code,
                Package = Text(row.Cell(colPackage)) ?? string.Empty,
                Type = type,
                Start = row.Cell(colStart).GetDateTime() ?? default,
                Finish = row.Cell(colFinish).GetDateTime() ?? default,
            });
        }
        return rows;
    }

    public static IReadOnlyList<StatusMapping> SeededStatusMappings() => SeedData.StatusMappings
        .Select(m => new StatusMapping
        {
            AconexStatus = m.AconexStatus,
            Status = m.Status,
            IsLegacy = m.IsLegacy,
        })
        .ToList();

    // ------------------------------------------------------------------ helpers

    public static int HeaderRow(IExcelSheet sheet, string anyHeader) =>
        sheet.FindRowContainingAnywhere(anyHeader)
        ?? throw new InvalidOperationException($"Sheet '{sheet.Name}' has no '{anyHeader}' header");

    public static int Column(IExcelSheet sheet, int headerRow, string header)
    {
        var row = sheet.Row(headerRow);
        for (var c = 1; c <= sheet.ColumnCount; c++)
        {
            if (string.Equals(row.Cell(c).GetStringOrNull()?.Trim(), header, StringComparison.OrdinalIgnoreCase))
            {
                return c;
            }
        }
        throw new InvalidOperationException($"Sheet '{sheet.Name}' has no '{header}' column");
    }

    // Blank cells and the "0" the export writes for a missing transmittal are null.
    public static string? Text(IExcelCell cell)
    {
        var value = cell.GetStringOrNull()?.Trim();
        return string.IsNullOrEmpty(value) || value == "0" ? null : value;
    }
}
