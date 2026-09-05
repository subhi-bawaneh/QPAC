using Dip.Application.Engine;
using FluentAssertions;
using Xunit;

namespace Dip.Engine.Tests;

public class WeekCalendarTests
{
    // docs/excel-analysis.md § 4.4, formula 1: date + (7 - WEEKDAY(date, 2)) days,
    // at 23:59:59. WEEKDAY(...,2) is Monday=1..Sunday=7.
    [Theory]
    [InlineData("2025-11-04", "2025-11-09")]   // Tuesday   -> the coming Sunday
    [InlineData("2025-11-03", "2025-11-09")]   // Monday
    [InlineData("2025-11-08", "2025-11-09")]   // Saturday
    [InlineData("2025-11-09", "2025-11-09")]   // Sunday stays on itself
    [InlineData("2026-08-30", "2026-08-30")]   // the sample report date, a Sunday
    public void WeekEnd_LandsOnSundayAtEndOfDay(string input, string expectedDate)
    {
        var result = WeekCalendar.WeekEnd(DateTime.Parse(input));

        result.Date.Should().Be(DateTime.Parse(expectedDate));
        result.TimeOfDay.Should().Be(new TimeSpan(23, 59, 59));
        result.DayOfWeek.Should().Be(DayOfWeek.Sunday);
    }

    [Fact]
    public void WeekEnd_IgnoresTheTimeOfDayOfTheInput()
    {
        var morning = WeekCalendar.WeekEnd(new DateTime(2026, 8, 30, 6, 0, 0));
        var evening = WeekCalendar.WeekEnd(new DateTime(2026, 8, 30, 22, 30, 0));

        morning.Should().Be(evening);
    }

    [Fact]
    public void WeekStart_IsSevenDaysBeforeTheEnd()
    {
        var end = WeekCalendar.WeekEnd(new DateTime(2025, 11, 2));

        WeekCalendar.WeekStart(end).Should().Be(end.AddDays(-7));
    }

    // Week 1 ends on the start week; the sample sheet's week 1 is 2025-10-26 -> 2025-11-02.
    [Fact]
    public void Weeks_NumbersFromOneAndStepsBySevenDays()
    {
        var start = WeekCalendar.WeekEnd(new DateTime(2025, 11, 2));
        var end = WeekCalendar.WeekEnd(new DateTime(2025, 11, 30));

        var weeks = WeekCalendar.Weeks(start, end).ToList();

        weeks.Should().HaveCount(5);
        weeks[0].Number.Should().Be(1);
        weeks[0].From.Date.Should().Be(new DateTime(2025, 10, 26));
        weeks[0].To.Date.Should().Be(new DateTime(2025, 11, 2));
        weeks[1].To.Date.Should().Be(new DateTime(2025, 11, 9));
        weeks[^1].To.Date.Should().Be(new DateTime(2025, 11, 30));
    }

    [Fact]
    public void Weeks_EndBeforeStart_YieldsNothing()
    {
        var start = WeekCalendar.WeekEnd(new DateTime(2026, 1, 11));

        WeekCalendar.Weeks(start, start.AddDays(-7)).Should().BeEmpty();
    }
}
