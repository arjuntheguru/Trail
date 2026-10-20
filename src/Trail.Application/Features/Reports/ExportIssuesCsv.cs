using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Issues;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Reports;

/// <summary>From/To bound the creation date (UTC, inclusive) and span at most 93 days.</summary>
public sealed record ExportIssuesQuery(DateOnly? From = null, DateOnly? To = null, Guid? SiteId = null, IssueStatus? Status = null);

public sealed class ExportIssuesCsvHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<CsvExport>> Handle(ExportIssuesQuery query, CancellationToken ct)
    {
        var range = ReportRange.Resolve(query.From, query.To, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        if (range.IsFailure) return range.Error!;
        var fromUtc = range.Value.FromUtc;
        var toUtc = range.Value.ToExclusiveUtc;

        var scope = await access.GetScopeAsync(ct);
        if (await ReportScope.CheckSiteAsync(db, scope, query.SiteId, ct) is { } error) return error;

        var issues = IssueRules.InScope(db.Issues.AsNoTracking(), scope).Where(i => i.CreatedAt >= fromUtc && i.CreatedAt < toUtc);
        if (query.SiteId is { } siteId) issues = issues.Where(i => i.SiteId == siteId);
        if (query.Status is { } status) issues = issues.Where(i => i.Status == status);

        if (await issues.CountAsync(ct) > ReportScope.MaxExportRows) return ReportScope.TooManyRows;

        var sites = db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete]);

        var rows = issues
            .OrderBy(i => i.CreatedAt).ThenBy(i => i.Number)
            .Select(i => new
            {
                i.Number, i.Title,
                SiteName = sites.Where(s => s.Id == i.SiteId).Select(s => s.Name).FirstOrDefault(),
                i.Priority, i.Status, i.Source,
                Assignee = db.Memberships.Where(m => m.Id == i.AssigneeId).Select(m => m.User.FirstName + " " + m.User.LastName).FirstOrDefault(),
                i.CreatedAt, i.DueAt, i.ResolvedAt, i.Resolution, i.ClosedAt,
            });

        return new CsvExport($"issues-{range.Value.FileSuffix}.csv", async (stream, token) =>
        {
            await using var csv = new CsvWriter(stream);
            await csv.WriteRowAsync("Number", "Title", "Site", "Priority", "Status", "Source", "Assignee", "Created", "Due", "Resolved", "Resolution", "Closed");
            await foreach (var r in rows.AsAsyncEnumerable().WithCancellation(token))
                await csv.WriteRowAsync(
                    r.Number, r.Title, r.SiteName, r.Priority, r.Status, r.Source, r.Assignee?.Trim(),
                    r.CreatedAt, r.DueAt, r.ResolvedAt, r.Resolution, r.ClosedAt);
        });
    }
}
