using Trail.Application.Common.Results;

namespace Trail.Application.Features.Reports;

public readonly record struct ReportDateRange(DateOnly From, DateOnly To)
{
    /// <summary>Start of the first day, UTC.</summary>
    public DateTime FromUtc => From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    /// <summary>Start of the day after the last one, UTC (exclusive bound).</summary>
    public DateTime ToExclusiveUtc => To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public string FileSuffix => $"{From:yyyy-MM-dd}-to-{To:yyyy-MM-dd}";
}

public static class ReportRange
{
    public const int MaxDays = 93;
    public const int DefaultDays = 30;

    public static readonly Error Backwards = Error.Validation("report.invalid_range", "The end date must not be before the start date.");

    public static readonly Error TooLong =
        Error.Validation("report.invalid_range", $"The date range is limited to {MaxDays} days.");

    /// <summary>A missing end is today (UTC); a missing start is 30 days before the end. Both bounds are inclusive.</summary>
    public static Result<ReportDateRange> Resolve(DateOnly? from, DateOnly? to, DateOnly today)
    {
        var end = to ?? today;
        var start = from ?? end.AddDays(-DefaultDays);
        if (end < start) return Backwards;
        if (end.DayNumber - start.DayNumber + 1 > MaxDays) return TooLong;
        return new ReportDateRange(start, end);
    }
}
