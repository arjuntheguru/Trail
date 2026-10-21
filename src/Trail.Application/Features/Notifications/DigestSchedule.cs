using Trail.Domain.Common;

namespace Trail.Application.Features.Notifications;

/// <summary>When a person's daily digest is due. Pure.</summary>
public static class DigestSchedule
{
    /// <summary>The time zone most of the person's sites are in (ties: alphabetical), else the organization's.</summary>
    public static string ZoneFor(IEnumerable<string> siteTimeZones, string organizationTimeZone) =>
        siteTimeZones.GroupBy(z => z, StringComparer.Ordinal)
            .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => g.Key).FirstOrDefault() ?? organizationTimeZone;

    /// <summary>Due once the local clock has reached <paramref name="digestHour"/> and no digest ran on the local date yet.</summary>
    public static bool IsDue(DateTime utcNow, string timeZone, int digestHour, DateOnly? lastDigestOn, out DateOnly localToday)
    {
        var zone = SiteTime.Zone(timeZone);
        localToday = SiteTime.Today(utcNow, zone);
        var localHour = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc), zone).Hour;
        return localHour >= digestHour && lastDigestOn != localToday;
    }
}
