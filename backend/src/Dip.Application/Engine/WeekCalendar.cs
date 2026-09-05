namespace Dip.Application.Engine;

// Reporting weeks end on Sunday at 23:59:59 (docs/excel-analysis.md § 4.4, formula 1:
// `date + (7 - WEEKDAY(date, 2)) days`). WEEKDAY(...,2) counts Monday=1..Sunday=7, so
// a Sunday adds zero days and stays on itself — Corporate Summary!C5 shows exactly
// that for the Sunday report date 2026-08-30.
public static class WeekCalendar
{
    private static readonly TimeSpan EndOfDay = new(23, 59, 59);

    public static DateTime WeekEnd(DateTime date)
    {
        var isoDay = date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)date.DayOfWeek;
        return date.Date.AddDays(7 - isoDay) + EndOfDay;
    }

    // Weeks are half-open on the left: a week covers (WeekEnd - 7 days, WeekEnd].
    public static DateTime WeekStart(DateTime weekEnd) => weekEnd.AddDays(-7);

    // Week 1 ends on startWeek; each following week ends 7 days later, up to endWeek.
    public static IEnumerable<(int Number, DateTime From, DateTime To)> Weeks(
        DateTime startWeek, DateTime endWeek)
    {
        var number = 1;
        for (var to = startWeek; to <= endWeek; to = to.AddDays(7))
        {
            yield return (number++, WeekStart(to), to);
        }
    }
}
