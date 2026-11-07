namespace Trail.Domain.Tasks;

public enum Frequency
{
    Once,
    Daily,
    Weekly,
    Monthly,
    Yearly,
}

[Flags]
public enum WeekDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
    Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
    Weekend = Saturday | Sunday,
    All = Weekdays | Weekend,
}

/// <summary>
/// When a scheduled task falls due, in the site's local calendar. Examples: daily; every weekday; every 2 weeks on
/// Monday and Thursday; monthly on the 15th; every 6 months; on the last day of each month (<c>DayOfMonth = -1</c>).
/// Days past the end of a short month fall on its last day.
/// </summary>
public sealed record Recurrence
{
    public const int LastDayOfMonth = -1;
    public const int MaxInterval = 52;

    public required Frequency Frequency { get; init; }

    /// <summary>Every N days/weeks/months/years.</summary>
    public int Interval { get; init; } = 1;

    public required DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }

    /// <summary>Weekly only. Defaults to the start date's weekday.</summary>
    public WeekDays Days { get; init; } = WeekDays.None;

    /// <summary>Monthly/yearly only: 1-31, or -1 for the last day. Defaults to the start date's day.</summary>
    public int? DayOfMonth { get; init; }

    public IReadOnlyList<string> Validate()
    {
        var errors = new List<string>();
        if (Interval is < 1 or > MaxInterval) errors.Add($"Interval must be between 1 and {MaxInterval}.");
        if (EndDate is { } end && end < StartDate) errors.Add("End date cannot be before the start date.");
        if (Days != WeekDays.None && Frequency != Frequency.Weekly) errors.Add("Days of the week only apply to weekly schedules.");
        if ((Days & ~WeekDays.All) != 0) errors.Add("Unknown day of the week.");
        if (DayOfMonth is { } d)
        {
            if (Frequency is not (Frequency.Monthly or Frequency.Yearly)) errors.Add("Day of month only applies to monthly and yearly schedules.");
            if (d is not (LastDayOfMonth or >= 1 and <= 31)) errors.Add("Day of month must be 1-31 or -1 for the last day.");
        }
        return errors;
    }

    public bool OccursOn(DateOnly date)
    {
        if (date < StartDate || (EndDate is { } end && date > end)) return false;

        return Frequency switch
        {
            Frequency.Once => date == StartDate,
            Frequency.Daily => (date.DayNumber - StartDate.DayNumber) % Interval == 0,
            Frequency.Weekly => WeeksBetween(StartDate, date) % Interval == 0 && EffectiveDays.HasFlag(ToFlag(date.DayOfWeek)),
            Frequency.Monthly => MonthsBetween(StartDate, date) % Interval == 0 && date.Day == DueDayIn(date.Year, date.Month),
            Frequency.Yearly => (date.Year - StartDate.Year) % Interval == 0
                                && date.Month == StartDate.Month
                                && date.Day == DueDayIn(date.Year, date.Month),
            _ => false,
        };
    }

    /// <summary>Occurrences from <paramref name="from"/> to <paramref name="to"/> inclusive.</summary>
    public IEnumerable<DateOnly> Occurrences(DateOnly from, DateOnly to)
    {
        for (var date = from > StartDate ? from : StartDate; date <= to; date = date.AddDays(1))
            if (OccursOn(date)) yield return date;
    }

    private WeekDays EffectiveDays => Days == WeekDays.None ? ToFlag(StartDate.DayOfWeek) : Days;

    private int DueDayIn(int year, int month)
    {
        var days = DateTime.DaysInMonth(year, month);
        var wanted = DayOfMonth ?? StartDate.Day;
        return wanted == LastDayOfMonth || wanted > days ? days : wanted;
    }

    /// <summary>Whole weeks between the Monday-starting weeks containing each date.</summary>
    private static int WeeksBetween(DateOnly start, DateOnly date) =>
        (MondayOf(date).DayNumber - MondayOf(start).DayNumber) / 7;

    private static DateOnly MondayOf(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    private static int MonthsBetween(DateOnly start, DateOnly date) =>
        (date.Year - start.Year) * 12 + date.Month - start.Month;

    public static WeekDays ToFlag(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => WeekDays.Monday,
        DayOfWeek.Tuesday => WeekDays.Tuesday,
        DayOfWeek.Wednesday => WeekDays.Wednesday,
        DayOfWeek.Thursday => WeekDays.Thursday,
        DayOfWeek.Friday => WeekDays.Friday,
        DayOfWeek.Saturday => WeekDays.Saturday,
        _ => WeekDays.Sunday,
    };
}
