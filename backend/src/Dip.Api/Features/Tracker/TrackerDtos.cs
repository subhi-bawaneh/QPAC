using Dip.Application.Engine;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Tracker;

// One Tracker row as the grid renders it: the document's own columns plus the
// computed ones (Tracker.xlsx!Tracker, docs/excel-analysis.md § 4.1).
public sealed record TrackerRowDto(
    Guid DocumentId,
    string DocumentNumber,
    string Type,
    string Discipline,
    string Title,
    DateTime? DeliveryMilestone,
    string? ActivityId,
    string? PackageName,
    string Building,
    string Level,
    string Trade,
    string? Author,
    int? SubmissionsCount,
    string? Revision,
    string? AconexStatus,
    UnifiedStatus? Status,
    DateTime? SubmissionDate,
    DateTime? DateModified,
    string? Transmittal,
    DateTime? PlannedStart,
    DateTime? PlannedFinish,
    DateTime? ActualStart,
    DateTime? ActualFinish)
{
    public static TrackerRowDto From(Document document, TrackerRow row) => new(
        document.Id,
        document.DocumentNumber,
        document.F04DocType,
        document.CorporateDiscipline,
        document.Title,
        document.DeliveryMilestone,
        document.ActivityId,
        document.PackageName,
        document.F07Building,
        document.F08BLevel,
        document.F05Discipline,
        document.Exchanges.FirstOrDefault(e => e.Number == 1)?.Author,
        row.SubmissionsCount,
        row.Revision,
        row.AconexStatus,
        row.Status,
        row.SubmissionDate,
        row.DateModified,
        row.Transmittal,
        row.PlannedStart,
        row.PlannedFinish,
        row.ActualStart,
        row.ActualFinish);
}

// One Aconex revision of a document, for the detail drawer's revision history.
public sealed record TrackerRevisionDto(
    Guid Id,
    string Revision,
    string AconexStatus,
    UnifiedStatus? Status,
    string? ReviewStatus,
    DateTime DateModified,
    string? TransmittalIn,
    string FileType,
    string FileName,
    bool IsLatest,
    bool IsTerminated);

public sealed record TrackerDocumentDto(
    TrackerRowDto Row,
    IReadOnlyList<TrackerRevisionDto> Revisions);
