using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Api.Features.Tracker;

// One Tracker row as the grid renders it: the document's own columns plus the
// computed ones (Tracker.xlsx!Tracker, docs/excel-analysis.md § 4.1).
public sealed record TrackerRowDto(
    Guid DocumentId,
    DataTarget Layer,
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
    // Every column, display and computed alike, lives on the snapshot (decision D11),
    // so the grid never joins back to the source tables.
    public static TrackerRowDto From(DocumentSnapshot snapshot) => new(
        snapshot.DocumentId,
        snapshot.Layer,
        snapshot.DocumentNumber,
        snapshot.Type,
        snapshot.Discipline,
        snapshot.Title,
        snapshot.DeliveryMilestone,
        snapshot.ActivityId,
        snapshot.PackageName,
        snapshot.Building,
        snapshot.Level,
        snapshot.Trade,
        snapshot.Author,
        snapshot.SubmissionsCount,
        snapshot.Revision,
        snapshot.AconexStatus,
        snapshot.Status,
        snapshot.SubmissionDate,
        snapshot.DateModified,
        snapshot.Transmittal,
        snapshot.PlannedStart,
        snapshot.PlannedFinish,
        snapshot.ActualStart,
        snapshot.ActualFinish);
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
