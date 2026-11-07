using Trail.Application.Features.Schedules;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Tasks;

public sealed class RecurrenceSummaryTests
{
    private static readonly DateOnly Start = new(2026, 3, 9); // a Monday

    private static string Describe(Frequency frequency, int interval = 1, WeekDays days = WeekDays.None, int? dayOfMonth = null, DateOnly? end = null) =>
        RecurrenceSummary.Describe(new Recurrence
        {
            Frequency = frequency, Interval = interval, StartDate = Start, EndDate = end, Days = days, DayOfMonth = dayOfMonth,
        });

    [Fact]
    public void Daily() => Describe(Frequency.Daily).ShouldBe("Every day");

    [Fact]
    public void Daily_with_an_interval() => Describe(Frequency.Daily, 3).ShouldBe("Every 3 days");

    [Fact]
    public void Weekly_lists_the_days_in_week_order() =>
        Describe(Frequency.Weekly, 2, WeekDays.Thursday | WeekDays.Monday).ShouldBe("Every 2 weeks on Mon, Thu");

    [Fact]
    public void Weekly_without_days_uses_the_start_date_weekday() =>
        Describe(Frequency.Weekly).ShouldBe("Every week on Mon");

    [Fact]
    public void Monthly_on_a_day() => Describe(Frequency.Monthly, 1, dayOfMonth: 15).ShouldBe("Every month on day 15");

    [Fact]
    public void Monthly_defaults_to_the_start_day() => Describe(Frequency.Monthly, 6).ShouldBe("Every 6 months on day 9");

    [Fact]
    public void Monthly_on_the_last_day() =>
        Describe(Frequency.Monthly, dayOfMonth: Recurrence.LastDayOfMonth).ShouldBe("Every month on the last day");

    [Fact]
    public void Yearly() => Describe(Frequency.Yearly).ShouldBe("Every year on 9 Mar");

    [Fact]
    public void Once() => Describe(Frequency.Once).ShouldBe("Once on 9 Mar 2026");

    [Fact]
    public void An_end_date_is_appended() =>
        Describe(Frequency.Daily, end: new DateOnly(2026, 12, 31)).ShouldBe("Every day, until 31 Dec 2026");
}
