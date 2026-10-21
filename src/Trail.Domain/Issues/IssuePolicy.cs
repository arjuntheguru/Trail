namespace Trail.Domain.Issues;

public static class IssuePolicy
{
    /// <summary>How long a corrective action may take by default, by priority. People can change the due time.</summary>
    public static TimeSpan DefaultResponseTime(IssuePriority priority) => priority switch
    {
        IssuePriority.Critical => TimeSpan.FromHours(4),
        IssuePriority.High => TimeSpan.FromHours(24),
        IssuePriority.Medium => TimeSpan.FromDays(3),
        _ => TimeSpan.FromDays(7),
    };

    public static string Truncate(string text, int max) => text.Length <= max ? text : string.Concat(text.AsSpan(0, max - 1), "…");
}
