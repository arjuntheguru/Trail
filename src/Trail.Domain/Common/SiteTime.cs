namespace Trail.Domain.Common;

/// <summary>Converts between UTC instants and a site's local calendar, handling daylight-saving transitions.</summary>
public static class SiteTime
{
    public static TimeZoneInfo Zone(string ianaId) =>
        TimeZoneInfo.TryFindSystemTimeZoneById(ianaId, out var zone) ? zone : TimeZoneInfo.Utc;

    public static DateOnly Today(DateTime utcNow, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone));

    /// <summary>
    /// The UTC instant of a local wall-clock time. Times skipped by a spring-forward jump move forward by the
    /// gap (02:30 becomes 03:30); times repeated by a fall-back jump resolve to their first occurrence.
    /// </summary>
    public static DateTime ToUtc(DateOnly date, TimeOnly time, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);

        if (zone.IsInvalidTime(local))
        {
            var adjustment = zone.GetAdjustmentRules()
                .FirstOrDefault(r => r.DateStart <= local && r.DateEnd >= local)?.DaylightDelta ?? TimeSpan.FromHours(1);
            local = local.Add(adjustment);
        }

        if (zone.IsAmbiguousTime(local))
        {
            var offsets = zone.GetAmbiguousTimeOffsets(local);
            var first = offsets.Max(); // the earlier instant has the larger (daylight) offset
            return DateTime.SpecifyKind(local - first, DateTimeKind.Utc);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }
}
