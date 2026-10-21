using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>Tasks across the caller's sites. From/To bound the due date (inclusive) and span at most 93 days.</summary>
public sealed record ListTasksQuery(
    Guid? SiteId = null, SiteTaskStatus? Status = null, DateOnly? From = null, DateOnly? To = null,
    bool? HasFailures = null, bool? AwaitingApproval = null);

public sealed class ListTasksHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public const int MaxRangeDays = 93;

    public async Task<Result<PagedResult<TaskListItemResponse>>> Handle(ListTasksQuery query, PageRequest page, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // A missing bound is derived from the other so the range never exceeds the cap.
        var to = query.To ?? query.From?.AddDays(MaxRangeDays - 1) ?? DateOnly.FromDateTime(now).AddDays(1);
        var from = query.From ?? to.AddDays(-(MaxRangeDays - 1));
        if (to < from)
            return Error.Validation("task.invalid_range", "The end date must not be before the start date.");
        if (to.DayNumber - from.DayNumber + 1 > MaxRangeDays)
            return Error.Validation("task.invalid_range", $"The date range is limited to {MaxRangeDays} days.");

        var scope = await access.GetScopeAsync(ct);
        var tasks = db.SiteTasks.AsNoTracking().InScope(scope).Where(t => t.DueDate >= from && t.DueDate <= to);

        if (query.SiteId is { } siteId) tasks = tasks.Where(t => t.SiteId == siteId);
        if (query.Status is { } status) tasks = tasks.Where(t => t.Status == status);
        if (query.HasFailures is { } hasFailures) tasks = tasks.Where(t => t.HasFailures == hasFailures);
        if (query.AwaitingApproval is { } awaiting)
        {
            tasks = awaiting
                ? tasks.Where(t => t.Status == SiteTaskStatus.Completed && t.RequiresApproval && t.ApprovedAt == null)
                : tasks.Where(t => !(t.Status == SiteTaskStatus.Completed && t.RequiresApproval && t.ApprovedAt == null));
        }

        var paged = await tasks
            .OrderByDescending(t => t.DueAt).ThenBy(t => t.Title).ThenBy(t => t.Id)
            .ToRows()
            .ToPagedResultAsync(page, ct);

        return new PagedResult<TaskListItemResponse>(
            await TaskMapping.ToItemsAsync(db, paged.Items, now, ct), paged.Page, paged.PageSize, paged.TotalCount);
    }
}
