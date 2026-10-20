using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>The columns a list needs; the field snapshot and entries stay in the database.</summary>
internal sealed record TaskRow(
    Guid Id, Guid SiteId, string Title, TaskKind Kind, SiteTaskStatus Status, DateOnly DueDate, DateTime DueAt,
    DateTime? AvailableAt, bool RequiresApproval, bool RequiresPhoto, Guid? DepartmentId, SiteTaskSource Source,
    int EntryCount, DateTime? CompletedAt, Guid? CompletedById, bool CompletedLate, bool HasFailures, DateTime? ApprovedAt);

internal static class TaskMapping
{
    public static IQueryable<SiteTask> InScope(this IQueryable<SiteTask> tasks, SiteScope scope)
    {
        if (scope.AllSites) return tasks;
        if (scope.SiteIds.Count == 0) return tasks.Where(_ => false);

        var ids = scope.SiteIds.ToList();
        return tasks.Where(t => ids.Contains(t.SiteId));
    }

    public static IQueryable<TaskRow> ToRows(this IQueryable<SiteTask> tasks) => tasks.Select(t => new TaskRow(
        t.Id, t.SiteId, t.Title, t.Kind, t.Status, t.DueDate, t.DueAt, t.AvailableAt, t.RequiresApproval,
        t.RequiresPhoto, t.DepartmentId, t.Source, t.Entries.Count(e => e.SubmittedAt != null), t.CompletedAt,
        t.CompletedById, t.CompletedLate, t.HasFailures, t.ApprovedAt));

    public static bool CanCompleteLate(SiteTaskStatus status, DateTime dueAt, DateTime now) =>
        status == SiteTaskStatus.Missed && now <= dueAt + SiteTask.LateCompletionWindow;

    public static async Task<IReadOnlyList<TaskListItemResponse>> ToItemsAsync(
        IAppDbContext db, IReadOnlyList<TaskRow> rows, DateTime now, CancellationToken ct)
    {
        if (rows.Count == 0) return [];

        var siteIds = rows.Select(r => r.SiteId).Distinct().ToList();
        var siteNames = await db.Sites.AsNoTracking().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => siteIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var names = await UserNamesAsync(db, rows.Where(r => r.CompletedById != null).Select(r => r.CompletedById!.Value), ct);

        return rows.Select(r => new TaskListItemResponse(
            r.Id, r.SiteId, siteNames.GetValueOrDefault(r.SiteId, string.Empty), r.Title, r.Kind, r.Status, r.DueDate,
            r.DueAt, r.AvailableAt,
            IsOverdue: r.Status == SiteTaskStatus.Open && now > r.DueAt,
            IsAwaitingApproval: r.Status == SiteTaskStatus.Completed && r.RequiresApproval && r.ApprovedAt is null,
            r.RequiresApproval, r.RequiresPhoto, r.DepartmentId, r.Source, r.EntryCount, r.CompletedAt,
            r.CompletedById is { } by ? names.GetValueOrDefault(by) : null,
            r.CompletedLate, r.HasFailures, CanCompleteLate(r.Status, r.DueAt, now))).ToList();
    }

    /// <summary>Builds the full response for a task whose <see cref="SiteTask.Entries"/> are loaded.</summary>
    public static async Task<TaskDetailResponse> ToDetailAsync(IAppDbContext db, SiteTask task, DateTime now, CancellationToken ct)
    {
        var site = await db.Sites.AsNoTracking().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => s.Id == task.SiteId)
            .Select(s => new { s.Name, s.TimeZone })
            .FirstAsync(ct);

        var userIds = task.Entries.Where(e => e.SubmittedById != null).Select(e => e.SubmittedById!.Value)
            .Concat(new[] { task.CompletedById, task.ApprovedById }.OfType<Guid>());
        var names = await UserNamesAsync(db, userIds, ct);
        string? Name(Guid? id) => id is { } value ? names.GetValueOrDefault(value) : null;

        var entries = task.Entries
            .OrderBy(e => e.CreatedAt).ThenBy(e => e.SubmittedAt)
            .Select(e => new TaskEntryResponse(
                e.Id, e.Answers, e.PhotoIds, e.Note, e.Failures, e.HasFailures, e.SubmittedAt, e.SubmittedById, Name(e.SubmittedById)))
            .ToList();

        return new TaskDetailResponse(
            task.Id, task.SiteId, site.Name, site.TimeZone, task.Title, task.Description, task.Kind, task.Status, task.Source,
            task.Fields, task.RequiresApproval, task.RequiresPhoto, task.DepartmentId, task.DueDate, task.DueAt, task.AvailableAt,
            task.IsOverdue(now), task.IsAwaitingApproval, CanCompleteLate(task.Status, task.DueAt, now), task.SnoozeCount,
            task.CompletedAt, task.CompletedById, Name(task.CompletedById), task.CompletedLate, task.HasFailures,
            task.ApprovedAt, task.ApprovedById, Name(task.ApprovedById), task.ApprovalNote, entries);
    }

    private static async Task<Dictionary<Guid, string>> UserNamesAsync(IAppDbContext db, IEnumerable<Guid> ids, CancellationToken ct)
    {
        var distinct = ids.Distinct().ToList();
        if (distinct.Count == 0) return [];

        var users = await db.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToListAsync(ct);
        return users.ToDictionary(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim());
    }
}
