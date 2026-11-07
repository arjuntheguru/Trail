using System.Text.Json;
using Trail.Domain.Common;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Integrations;

/// <summary>Per-reading rules of the sensor endpoint. Pure so the mapping to record log entries is unit-tested.</summary>
public static class SensorReadingRules
{
    public static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);
    public static readonly TimeSpan MaxFutureSkew = TimeSpan.FromMinutes(5);
    public const int MaxReadingsPerRequest = 500;

    /// <summary>Readings are kept to whole seconds so a retried reading compares equal after a database round trip.</summary>
    public static DateTime Normalize(DateTime utc) =>
        new(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    /// <summary>A rejection code when the reading's time is unacceptable, else null.</summary>
    public static string? CheckTime(DateTime recordedAtUtc, DateTime nowUtc)
    {
        if (nowUtc - recordedAtUtc > MaxAge) return SensorResults.TooOld;
        if (recordedAtUtc - nowUtc > MaxFutureSkew) return SensorResults.InFuture;
        return null;
    }

    /// <summary>The site-local calendar day of the reading; it decides which day's record log it belongs to.</summary>
    public static DateOnly LocalDate(DateTime recordedAtUtc, string siteTimeZone) =>
        SiteTime.Today(recordedAtUtc, SiteTime.Zone(siteTimeZone));

    /// <summary>The single answer a reading provides: the temperature field's value. No comment, no photo.</summary>
    public static Dictionary<string, FieldAnswer> ToAnswers(string fieldKey, decimal value) => new()
    {
        [fieldKey] = new FieldAnswer { Value = JsonSerializer.SerializeToElement(value) },
    };

    public static string Note(string deviceName) =>
        Truncate($"Recorded by sensor {deviceName}", SiteTask.NoteMaxLength);

    private static string Truncate(string text, int max) => text.Length <= max ? text : text[..max];
}

/// <summary>Result codes returned per reading: "accepted", "skipped:..." (valid but not stored) or "rejected:...".</summary>
public static class SensorResults
{
    public const string Accepted = "accepted";

    public const string NoOpenTask = "skipped:no_open_task";
    public const string TaskNotAvailable = "skipped:task_not_available";
    public const string Duplicate = "skipped:duplicate";

    public const string UnknownDevice = "rejected:unknown_device";
    public const string SiteNotAllowed = "rejected:site_not_allowed";
    public const string TooOld = "rejected:too_old";
    public const string InFuture = "rejected:in_future";
    public const string InvalidReading = "rejected:invalid_reading";
}
