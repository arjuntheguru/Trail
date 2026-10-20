using Trail.Domain.Tasks;

namespace Trail.UnitTests.Tasks;

public sealed class RecurrenceTests
{
    private static DateOnly D(string iso) => DateOnly.Parse(iso, System.Globalization.CultureInfo.InvariantCulture);

    private static List<string> Dates(Recurrence r, string from, string to) =>
        r.Occurrences(D(from), D(to)).Select(d => d.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).ToList();

    [Fact]
    public void Daily_every_three_days_counts_from_the_start_date()
    {
        var r = new Recurrence { Frequency = Frequency.Daily, Interval = 3, StartDate = D("2026-03-01") };

        Dates(r, "2026-02-25", "2026-03-10").ShouldBe(["2026-03-01", "2026-03-04", "2026-03-07", "2026-03-10"]);
    }

    [Fact]
    public void Weekdays_only()
    {
        var r = new Recurrence { Frequency = Frequency.Weekly, StartDate = D("2026-03-02"), Days = WeekDays.Weekdays };

        Dates(r, "2026-03-02", "2026-03-09").ShouldBe(["2026-03-02", "2026-03-03", "2026-03-04", "2026-03-05", "2026-03-06", "2026-03-09"]);
    }

    [Fact]
    public void Fortnightly_on_two_days_skips_alternate_weeks_even_when_starting_midweek()
    {
        // Start on Wednesday 4 March; the start week counts as week 0 (its Monday is 2 March).
        var r = new Recurrence
        {
            Frequency = Frequency.Weekly, Interval = 2, StartDate = D("2026-03-04"), Days = WeekDays.Monday | WeekDays.Thursday,
        };

        Dates(r, "2026-03-01", "2026-03-31").ShouldBe(["2026-03-05", "2026-03-16", "2026-03-19", "2026-03-30"]);
    }

    [Fact]
    public void Weekly_defaults_to_the_start_dates_weekday()
    {
        var r = new Recurrence { Frequency = Frequency.Weekly, StartDate = D("2026-03-06") }; // Friday

        Dates(r, "2026-03-01", "2026-03-21").ShouldBe(["2026-03-06", "2026-03-13", "2026-03-20"]);
    }

    [Fact]
    public void Monthly_on_the_31st_falls_on_the_last_day_of_short_months()
    {
        var r = new Recurrence { Frequency = Frequency.Monthly, StartDate = D("2026-01-31") };

        Dates(r, "2026-01-01", "2026-05-31").ShouldBe(["2026-01-31", "2026-02-28", "2026-03-31", "2026-04-30", "2026-05-31"]);
    }

    [Fact]
    public void Monthly_last_day_handles_leap_years()
    {
        var r = new Recurrence { Frequency = Frequency.Monthly, StartDate = D("2028-01-10"), DayOfMonth = Recurrence.LastDayOfMonth };

        Dates(r, "2028-02-01", "2028-03-31").ShouldBe(["2028-02-29", "2028-03-31"]);
    }

    [Fact]
    public void Every_six_months()
    {
        var r = new Recurrence { Frequency = Frequency.Monthly, Interval = 6, StartDate = D("2026-01-15") };

        Dates(r, "2026-01-01", "2027-12-31").ShouldBe(["2026-01-15", "2026-07-15", "2027-01-15", "2027-07-15"]);
    }

    [Fact]
    public void Yearly_on_29_february_uses_28_february_in_other_years()
    {
        var r = new Recurrence { Frequency = Frequency.Yearly, StartDate = D("2028-02-29") };

        Dates(r, "2028-01-01", "2030-12-31").ShouldBe(["2028-02-29", "2029-02-28", "2030-02-28"]);
    }

    [Fact]
    public void Once_and_end_dates_are_respected()
    {
        new Recurrence { Frequency = Frequency.Once, StartDate = D("2026-05-01") }.Occurrences(D("2026-04-01"), D("2026-06-01"))
            .ShouldBe([D("2026-05-01")]);
        new Recurrence { Frequency = Frequency.Daily, StartDate = D("2026-05-01"), EndDate = D("2026-05-03") }
            .Occurrences(D("2026-04-01"), D("2026-06-01")).Count().ShouldBe(3);
    }

    [Theory]
    [InlineData(Frequency.Daily, 0, null, WeekDays.None, "Interval")]
    [InlineData(Frequency.Daily, 1, 5, WeekDays.None, "Day of month")]
    [InlineData(Frequency.Monthly, 1, 32, WeekDays.None, "Day of month must be")]
    [InlineData(Frequency.Daily, 1, null, WeekDays.Monday, "Days of the week")]
    public void Invalid_recurrences_are_reported(Frequency frequency, int interval, int? dayOfMonth, WeekDays days, string expected)
    {
        var r = new Recurrence { Frequency = frequency, Interval = interval, StartDate = D("2026-01-01"), DayOfMonth = dayOfMonth, Days = days };

        r.Validate().ShouldContain(e => e.Contains(expected));
    }
}
