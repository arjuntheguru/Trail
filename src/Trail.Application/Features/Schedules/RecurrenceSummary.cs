using System.Globalization;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Schedules;

/// <summary>Turns a recurrence into a short sentence for lists, e.g. "Every 2 weeks on Mon, Thu".</summary>
public static class RecurrenceSummary
{
    private static readonly (WeekDays Flag, string Name)[] DayNames =
    [
        (WeekDays.Monday, "Mon"), (WeekDays.Tuesday, "Tue"), (WeekDays.Wednesday, "Wed"), (WeekDays.Thursday, "Thu"),
        (WeekDays.Friday, "Fri"), (WeekDays.Saturday, "Sat"), (WeekDays.Sunday, "Sun"),
    ];

    public static string Describe(Recurrence r)
    {
        var text = r.Frequency switch
        {
            Frequency.Once => $"Once on {Format(r.StartDate)}",
            Frequency.Daily => Every(r.Interval, "day"),
            Frequency.Weekly => $"{Every(r.Interval, "week")} on {WeekdayList(r)}",
            Frequency.Monthly => $"{Every(r.Interval, "month")} on {MonthDay(r)}",
            Frequency.Yearly => $"{Every(r.Interval, "year")} on {r.StartDate.ToString("d MMM", CultureInfo.InvariantCulture)}"
                                + (r.DayOfMonth is { } d && d != r.StartDate.Day ? $" (day {DayText(d)})" : ""),
            _ => r.Frequency.ToString(),
        };
        return r.EndDate is { } end ? $"{text}, until {Format(end)}" : text;
    }

    private static string Every(int interval, string unit) => interval <= 1 ? $"Every {unit}" : $"Every {interval} {unit}s";

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static string WeekdayList(Recurrence r)
    {
        var days = r.Days == WeekDays.None ? Recurrence.ToFlag(r.StartDate.DayOfWeek) : r.Days;
        return string.Join(", ", DayNames.Where(d => days.HasFlag(d.Flag)).Select(d => d.Name));
    }

    private static string MonthDay(Recurrence r) =>
        (r.DayOfMonth ?? r.StartDate.Day) == Recurrence.LastDayOfMonth ? "the last day" : $"day {r.DayOfMonth ?? r.StartDate.Day}";

    private static string DayText(int day) => day == Recurrence.LastDayOfMonth ? "last" : day.ToString(CultureInfo.InvariantCulture);
}
