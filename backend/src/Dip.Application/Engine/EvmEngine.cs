using Dip.Domain.Entities;

namespace Dip.Application.Engine;

// Earned Value for one group (a discipline, or the whole project).
// CPI needs actual hours, which the platform does not collect yet (PLAN.md § 7),
// so it is always null — the shape is here so the dashboard doesn't change later.
public sealed record EvmRow(
    string Name,
    int Documents,
    decimal PlannedValue,
    decimal EarnedValue,
    decimal BudgetAtCompletion,
    decimal? SchedulePerformanceIndex,
    decimal ScheduleVariance,
    decimal PlannedPercent,
    decimal EarnedPercent,
    decimal? CostPerformanceIndex);

public sealed record EvmSummary(
    DateTime ReportDate,
    EvmRow Total,
    IReadOnlyList<EvmRow> Disciplines);

// SPI = EV / PV over the weighted document budget (PLAN.md § 7). Pure: no I/O.
//
// Each document contributes its BudgetWeight (default: the Exchange 01 duration in
// days) times a factor for how far it is planned to have got, and how far it
// actually got, as at the report date.
public static class EvmEngine
{
    public const string TotalRowName = "Total";

    public static EvmSummary Compute(
        IReadOnlyList<Document> documents,
        IReadOnlyList<TrackerRow> trackerRows,
        Project project,
        DateTime? reportDateOverride = null)
    {
        var reportDate = reportDateOverride ?? project.ReportDate ?? DateTime.Today;
        var rowsByDocument = trackerRows.ToDictionary(r => r.DocumentId);

        var entries = documents
            .Select(d => new Entry(
                Discipline: d.CorporateDiscipline?.Trim() ?? string.Empty,
                Weight: d.BudgetWeight,
                Row: rowsByDocument.TryGetValue(d.Id, out var row) ? row : null))
            .ToList();

        return new EvmSummary(
            ReportDate: reportDate,
            Total: Summarize(TotalRowName, entries, project, reportDate),
            Disciplines: entries
                .GroupBy(e => e.Discipline, StringComparer.Ordinal)
                .OrderBy(g => g.Key, StringComparer.Ordinal)
                .Select(g => Summarize(g.Key, g.ToList(), project, reportDate))
                .ToList());
    }

    private static EvmRow Summarize(
        string name, IReadOnlyList<Entry> entries, Project project, DateTime reportDate)
    {
        decimal plannedValue = 0;
        decimal earnedValue = 0;
        decimal budget = 0;

        foreach (var entry in entries)
        {
            budget += entry.Weight;
            plannedValue += entry.Weight * PlannedFactor(entry.Row, project, reportDate);
            earnedValue += entry.Weight * EarnedFactor(entry.Row, project, reportDate);
        }

        return new EvmRow(
            Name: name,
            Documents: entries.Count,
            PlannedValue: plannedValue,
            EarnedValue: earnedValue,
            BudgetAtCompletion: budget,
            // A group with nothing planned yet has no meaningful ratio.
            SchedulePerformanceIndex: plannedValue == 0 ? null : earnedValue / plannedValue,
            ScheduleVariance: earnedValue - plannedValue,
            PlannedPercent: budget == 0 ? 0m : plannedValue / budget,
            EarnedPercent: budget == 0 ? 0m : earnedValue / budget,
            // Needs actual hours; there is no such table yet (PLAN.md § 7).
            CostPerformanceIndex: null);
    }

    // Planned to be finished by the report date -> full weight; planned to have
    // started -> the Sub 1 weight; not started -> nothing.
    private static decimal PlannedFactor(TrackerRow? row, Project project, DateTime reportDate)
    {
        if (row?.PlannedFinish <= reportDate) return project.WeightApproved;
        if (row?.PlannedStart <= reportDate) return project.WeightSub1;
        return project.WeightPending;
    }

    // Approved by the report date -> full weight; otherwise credit for having been
    // submitted at least once. SubmissionsCount is the current revision count, so a
    // document submitted after the report date still earns its submission credit —
    // the same simplification the workbook makes.
    private static decimal EarnedFactor(TrackerRow? row, Project project, DateTime reportDate)
    {
        if (row?.ActualFinish <= reportDate) return project.WeightApproved;
        return row?.SubmissionsCount switch
        {
            >= 2 => project.WeightSub2,
            1 => project.WeightSub1,
            _ => project.WeightPending,
        };
    }

    private sealed record Entry(string Discipline, decimal Weight, TrackerRow? Row);
}
