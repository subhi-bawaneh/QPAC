using System.Globalization;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Engine;

// One row of the Tracker report — the computed state of a single document.
// Mirrors Tracker.xlsx!Tracker columns N..T and AB..AE (docs/excel-analysis.md § 4.1).
public sealed record TrackerRow(
    Guid DocumentId,
    string DocumentNumber,
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
    public DocumentSnapshot ToSnapshot(Guid projectId, DateTime computedAt) => new()
    {
        DocumentId = DocumentId,
        ProjectId = projectId,
        ComputedAt = computedAt,
        SubmissionsCount = SubmissionsCount,
        Revision = Revision,
        AconexStatus = AconexStatus,
        Status = Status,
        SubmissionDate = SubmissionDate,
        DateModified = DateModified,
        Transmittal = Transmittal,
        PlannedStart = PlannedStart,
        PlannedFinish = PlannedFinish,
        ActualStart = ActualStart,
        ActualFinish = ActualFinish,
    };
}

// Baseline lookups, prepared once per run: PlannedStart comes from the document's
// own Submittal activity, PlannedFinish from the Approval activity of the SAME
// PACKAGE — the pair is linked by Package, not by activity code (PLAN.md § 5.1.4).
public sealed class BaselinePlan
{
    private readonly IReadOnlyDictionary<string, BaselineActivity> _submittalByCode;
    private readonly IReadOnlyDictionary<string, BaselineActivity> _approvalByPackage;

    private BaselinePlan(
        IReadOnlyDictionary<string, BaselineActivity> submittalByCode,
        IReadOnlyDictionary<string, BaselineActivity> approvalByPackage)
    {
        _submittalByCode = submittalByCode;
        _approvalByPackage = approvalByPackage;
    }

    public static readonly BaselinePlan Empty = Create(Array.Empty<BaselineActivity>());

    public static BaselinePlan Create(IEnumerable<BaselineActivity> activities)
    {
        var byCode = new Dictionary<string, BaselineActivity>(StringComparer.OrdinalIgnoreCase);
        var byPackage = new Dictionary<string, BaselineActivity>(StringComparer.OrdinalIgnoreCase);

        // Submittal rows win the activity-code slot; an Approval-only code still
        // resolves so a document is never left unplanned by a data quirk.
        foreach (var activity in activities.OrderBy(a => a.Type == BaselineActivityType.Submittal ? 0 : 1))
        {
            if (!string.IsNullOrEmpty(activity.ActivityCode))
            {
                byCode.TryAdd(activity.ActivityCode, activity);
            }

            if (activity.Type == BaselineActivityType.Approval && !string.IsNullOrEmpty(activity.Package))
            {
                byPackage.TryAdd(activity.Package, activity);
            }
        }

        return new BaselinePlan(byCode, byPackage);
    }

    public (DateTime? PlannedStart, DateTime? PlannedFinish) For(string? activityId)
    {
        if (string.IsNullOrWhiteSpace(activityId)
            || !_submittalByCode.TryGetValue(activityId.Trim(), out var submittal))
        {
            return (null, null);
        }

        var finish = _approvalByPackage.TryGetValue(submittal.Package, out var approval)
            ? approval.Finish
            : (DateTime?)null;

        return (submittal.Finish, finish);
    }
}

// Per-document Tracker computation (PLAN.md § 5.4.1). Pure: no DbContext, no I/O.
public static class TrackerEngine
{
    public const string TerminatedStatus = "Terminated";

    public static IReadOnlyList<TrackerRow> Compute(
        IReadOnlyList<Document> documents,
        IReadOnlyList<AconexRevision> revisions,
        IReadOnlyList<BaselineActivity> baseline,
        IReadOnlyList<StatusMapping> statusMappings,
        Project project)
    {
        // Grouped once: the alternative is a scan of ~25k Aconex rows per document.
        //
        // Terminated revisions are excluded from every aggregation, matching the
        // workbook: Tracker.xlsx!SHD_History blanks the "Document No Final" of a
        // terminated row (600 of 600 in the sample), and every Tracker formula
        // matches on that column. Including them moves Submission Date and Actual
        // Start onto submissions that were withdrawn.
        var byDocumentNumber = revisions
            .Where(r => !string.IsNullOrEmpty(r.DocNoFinal) && !r.IsTerminated)
            .GroupBy(r => r.DocNoFinal, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<AconexRevision>)g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var statuses = StatusMappingLookup.Create(statusMappings);
        var plan = BaselinePlan.Create(baseline);

        var rows = new List<TrackerRow>(documents.Count);
        foreach (var document in documents)
        {
            var matches = byDocumentNumber.TryGetValue(document.DocumentNumber, out var found)
                ? found
                : Array.Empty<AconexRevision>();
            rows.Add(ComputeOne(document, matches, plan, statuses, project));
        }
        return rows;
    }

    public static TrackerRow ComputeOne(
        Document document,
        IReadOnlyList<AconexRevision> documentRevisions,
        BaselinePlan plan,
        StatusMappingLookup statuses,
        Project project)
    {
        var (plannedStart, plannedFinish) = project.ScheduleMode == ScheduleMode.Baseline
            ? plan.For(document.ActivityId)
            : (document.DeliveryMilestone,
               document.DeliveryMilestone?.AddDays(project.WorkingPlanApprovalDays));

        var latest = Latest(documentRevisions);
        if (latest is null)
        {
            return new TrackerRow(
                document.Id, document.DocumentNumber,
                SubmissionsCount: null, Revision: null, AconexStatus: null, Status: null,
                SubmissionDate: null, DateModified: null, Transmittal: null,
                plannedStart, plannedFinish, ActualStart: null, ActualFinish: null);
        }

        var aconexStatus = latest.IsTerminated ? TerminatedStatus : latest.AconexStatus;
        var status = statuses.Find(aconexStatus);
        var dateModified = latest.DateModified;

        // First time this revision was seen — not the first submission of the document.
        var submissionDate = documentRevisions
            .Where(r => string.Equals(r.Revision, latest.Revision, StringComparison.OrdinalIgnoreCase))
            .Min(r => r.DateModified);

        return new TrackerRow(
            document.Id,
            document.DocumentNumber,
            SubmissionsCount: SubmissionsFor(latest.Revision),
            Revision: latest.Revision,
            AconexStatus: aconexStatus,
            Status: status,
            SubmissionDate: submissionDate,
            DateModified: dateModified,
            Transmittal: Transmittal(latest.TransmittalIn),
            PlannedStart: plannedStart,
            PlannedFinish: plannedFinish,
            ActualStart: documentRevisions.Min(r => r.DateModified),
            ActualFinish: status == UnifiedStatus.Approved ? dateModified : null);
    }

    // MAXIFS over DateModified. Ties keep the first row in source order, matching
    // the workbook's "take the first match" behaviour.
    private static AconexRevision? Latest(IReadOnlyList<AconexRevision> revisions)
    {
        AconexRevision? latest = null;
        foreach (var revision in revisions)
        {
            if (latest is null || revision.DateModified > latest.DateModified)
            {
                latest = revision;
            }
        }
        return latest;
    }

    // Revision "00" is the first submission, so the count is the number + 1.
    // A non-numeric revision ("P00", "A") has no defined count.
    private static int? SubmissionsFor(string? revision) =>
        int.TryParse(revision, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value + 1
            : null;

    // The export writes 0 where there is no transmittal.
    private static string? Transmittal(string? transmittalIn)
    {
        var value = transmittalIn?.Trim();
        return string.IsNullOrEmpty(value) || value == "0" ? null : value;
    }
}
