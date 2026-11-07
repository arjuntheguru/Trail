using Trail.Domain.Issues;

namespace Trail.Application.Features.Reports;

/// <summary>One group of a compliance report. Key is the site or template id, or the ISO date when grouped by day.</summary>
public sealed record ComplianceRowResponse(
    string Key,
    string Name,
    int Due,
    int CompletedOnTime,
    int CompletedLate,
    int Missed,
    int WithFailures,
    int AwaitingApproval,
    decimal? Score);

public sealed record ComplianceReportResponse(
    DateOnly From,
    DateOnly To,
    ComplianceGrouping GroupBy,
    IReadOnlyList<ComplianceRowResponse> Rows,
    ComplianceRowResponse Total);

public sealed record DashboardSiteResponse(
    Guid SiteId,
    string SiteName,
    DateOnly Today,
    int DueToday,
    int Completed,
    int CompletedOnTime,
    int Overdue,
    int Remaining,
    int MissedYesterday,
    decimal? Score,
    int OpenIssues,
    int OverdueIssues,
    IReadOnlyDictionary<IssuePriority, int> OpenByPriority);

public sealed record DashboardTotalsResponse(
    int DueToday,
    int Completed,
    int CompletedOnTime,
    int Overdue,
    int Remaining,
    int MissedYesterday,
    decimal? Score,
    int OpenIssues,
    int OverdueIssues,
    IReadOnlyDictionary<IssuePriority, int> OpenByPriority);

public sealed record DashboardResponse(IReadOnlyList<DashboardSiteResponse> Sites, DashboardTotalsResponse Totals);

public sealed record ActivityItemResponse(
    string Kind,
    DateTime OccurredAt,
    Guid SiteId,
    string? SiteName,
    Guid? ActorId,
    string? ActorName,
    Guid SubjectId,
    string Title,
    int? IssueNumber);

public sealed record ActivityFeedResponse(IReadOnlyList<ActivityItemResponse> Items, DateTime? NextBefore);

/// <summary>A CSV file to stream: the handler has already checked the row count, so writing just runs the query.</summary>
public sealed record CsvExport(string FileName, Func<Stream, CancellationToken, Task> WriteAsync);
