using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Tasks;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Reports;

/// <summary>From/To bound the tasks' due date (inclusive) and span at most 93 days.</summary>
public sealed record ExportTasksQuery(DateOnly? From = null, DateOnly? To = null, Guid? SiteId = null, SiteTaskStatus? Status = null);

public sealed class ExportTasksCsvHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<CsvExport>> Handle(ExportTasksQuery query, CancellationToken ct)
    {
        var range = ReportRange.Resolve(query.From, query.To, DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime));
        if (range.IsFailure) return range.Error!;
        var (from, to) = range.Value;

        var scope = await access.GetScopeAsync(ct);
        if (await ReportScope.CheckSiteAsync(db, scope, query.SiteId, ct) is { } error) return error;

        var tasks = db.SiteTasks.AsNoTracking().InScope(scope).Where(t => t.DueDate >= from && t.DueDate <= to);
        if (query.SiteId is { } siteId) tasks = tasks.Where(t => t.SiteId == siteId);
        if (query.Status is { } status) tasks = tasks.Where(t => t.Status == status);

        if (await tasks.CountAsync(ct) > ReportScope.MaxExportRows) return ReportScope.TooManyRows;

        var sites = db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete]);

        var rows = tasks
            .OrderBy(t => t.DueDate).ThenBy(t => t.DueAt).ThenBy(t => t.Title).ThenBy(t => t.Id)
            .Select(t => new
            {
                SiteName = sites.Where(s => s.Id == t.SiteId).Select(s => s.Name).FirstOrDefault(),
                t.DueDate, t.Title, t.Status, t.DueAt, t.CompletedAt,
                CompletedBy = db.Users.Where(u => u.Id == t.CompletedById).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                t.CompletedLate, t.HasFailures, t.ApprovedAt,
                ApprovedBy = db.Users.Where(u => u.Id == t.ApprovedById).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
            });

        return new CsvExport($"tasks-{range.Value.FileSuffix}.csv", async (stream, token) =>
        {
            await using var csv = new CsvWriter(stream);
            await csv.WriteRowAsync("Site", "Date", "Title", "Status", "Due", "Completed at", "Completed by", "Late", "Failures", "Approved at", "Approved by");
            await foreach (var r in rows.AsAsyncEnumerable().WithCancellation(token))
                await csv.WriteRowAsync(
                    r.SiteName, r.DueDate, r.Title, r.Status, r.DueAt, r.CompletedAt, r.CompletedBy?.Trim(),
                    r.CompletedLate, r.HasFailures, r.ApprovedAt, r.ApprovedBy?.Trim());
        });
    }
}
