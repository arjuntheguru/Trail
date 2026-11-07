namespace Trail.Application.Common;

public static class TimeZones
{
    /// <summary>True for a known IANA time zone id such as "Europe/London". Windows display ids are rejected.</summary>
    public static bool IsValidIana(string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Length > 64 || id.Contains(' ')) return false;
        return TimeZoneInfo.TryFindSystemTimeZoneById(id, out _);
    }
}
