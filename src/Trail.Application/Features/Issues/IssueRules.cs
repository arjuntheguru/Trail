using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Common.Security;
using Trail.Domain.Files;
using Trail.Domain.Identity;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

/// <summary>Lookups, guards and the detail projection shared by the issue use cases.</summary>
internal static class IssueRules
{
    public static readonly Error NotFound = Error.NotFound("issue.not_found", "Issue not found.");

    public static readonly Error NotActive = Error.Conflict("issue.not_active", "The issue is already resolved or closed.");

    public static readonly Error NotOpen = Error.Conflict("issue.not_open", "Only an open issue can be started.");

    public static readonly Error AlreadyClosed = Error.Conflict("issue.already_closed", "The issue is already closed.");

    public static readonly Error NotResolvedOrClosed =
        Error.Conflict("issue.not_resolved_or_closed", "Only a resolved or closed issue can be reopened.");

    public static readonly Error NotAssignee =
        Error.Forbidden("issue.not_assignee", "Only the assignee or an issue manager can do this.");

    public static readonly Error UnknownAttachments = Error.Validation(
        "issue.unknown_attachments", "Every photo must be a photo uploaded for this issue's site.");

    public static readonly Error InvalidAssignee = Error.Validation(
        "issue.invalid_assignee", "The assignee must be an active member who can view issues at this site.");

    public static readonly Error DueInPast = Error.Validation("issue.due_in_past", "The due time must be in the future.");

    /// <summary>The issue (tracked), or null when it does not exist or its site is outside the caller's scope.</summary>
    public static async Task<Issue?> FindAsync(IAppDbContext db, ISiteAccess access, Guid id, CancellationToken ct)
    {
        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == id, ct);
        return issue is not null && (await access.GetScopeAsync(ct)).Includes(issue.SiteId) ? issue : null;
    }

    /// <summary>Restricts <paramref name="issues"/> to the sites inside <paramref name="scope"/>.</summary>
    public static IQueryable<Issue> InScope(IQueryable<Issue> issues, SiteScope scope)
    {
        if (scope.AllSites) return issues;
        if (scope.SiteIds.Count == 0) return issues.Where(_ => false);

        var ids = scope.SiteIds.ToList();
        return issues.Where(i => ids.Contains(i.SiteId));
    }

    public static bool CanActOn(ICurrentUser caller, Issue issue) =>
        (caller.MembershipId is { } membershipId && membershipId == issue.AssigneeId)
        || caller.HasPermission(Permissions.Issues.Manage);

    /// <summary>JSON gives unspecified kinds for offset-less values; treat those as UTC.</summary>
    public static DateTime AsUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
    };

    public static async Task<Error?> ValidatePhotosAsync(
        IAppDbContext db, IReadOnlyList<Guid>? photoIds, Guid siteId, CancellationToken ct)
    {
        if (photoIds is not { Count: > 0 }) return null;
        var distinct = photoIds.Distinct().ToList();
        if (distinct.Count > Issue.MaxPhotos) return UnknownAttachments;

        var found = await db.Attachments.CountAsync(
            a => distinct.Contains(a.Id) && a.Kind == AttachmentKind.Photo && a.SiteId == siteId, ct);
        return found == distinct.Count ? null : UnknownAttachments;
    }

    public static async Task<Error?> ValidateAssigneeAsync(IAppDbContext db, Guid assigneeId, Guid siteId, CancellationToken ct)
    {
        var membership = await db.Memberships.AsNoTracking()
            .Where(m => m.Id == assigneeId && m.Status == MembershipStatus.Active)
            .Select(m => new { m.AllSites, Permissions = m.Role.Permissions })
            .FirstOrDefaultAsync(ct);
        if (membership is null || !membership.Permissions.Contains(Permissions.Issues.View)) return InvalidAssignee;
        if (membership.AllSites) return null;

        // Sites may be soft-deleted while old issues still point at them; their region still counts.
        var regionId = await db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => s.Id == siteId).Select(s => s.RegionId).FirstOrDefaultAsync(ct);

        var reaches = await db.MembershipSites.AnyAsync(ms => ms.MembershipId == assigneeId && ms.SiteId == siteId, ct)
            || (regionId is { } region
                && await db.MembershipRegions.AnyAsync(mr => mr.MembershipId == assigneeId && mr.RegionId == region, ct));
        return reaches ? null : InvalidAssignee;
    }

    public static async Task<IssueDetailResponse> LoadDetailAsync(IAppDbContext db, Guid id, DateTime now, CancellationToken ct)
    {
        var issue = await db.Issues.AsNoTracking().Include(i => i.Events).SingleAsync(i => i.Id == id, ct);
        var events = issue.Events.OrderBy(e => e.OccurredAt).ThenBy(e => e.Id).ToList();

        var membershipIds = new HashSet<Guid>();
        if (issue.AssigneeId is { } assigneeId) membershipIds.Add(assigneeId);
        foreach (var e in events.Where(e => e.Kind == IssueEventKind.Assigned))
        {
            if (Guid.TryParse(e.From, out var from)) membershipIds.Add(from);
            if (Guid.TryParse(e.To, out var to)) membershipIds.Add(to);
        }
        var membershipNames = membershipIds.Count == 0
            ? []
            : await db.Memberships.AsNoTracking().Where(m => membershipIds.Contains(m.Id))
                .Select(m => new { m.Id, m.User.FirstName, m.User.LastName })
                .ToDictionaryAsync(m => m.Id, m => $"{m.FirstName} {m.LastName}".Trim(), ct);

        var userIds = events.Where(e => e.ActorId is not null).Select(e => e.ActorId!.Value)
            .Concat(new[] { issue.ResolvedById, issue.ClosedById }.OfType<Guid>())
            .ToHashSet();
        var userNames = userIds.Count == 0
            ? []
            : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.FirstName, u.LastName })
                .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        static string? NameOf(Dictionary<Guid, string> names, Guid? key) =>
            key is { } k && names.TryGetValue(k, out var name) ? name : null;
        string? MembershipName(string? raw) => Guid.TryParse(raw, out var key) ? NameOf(membershipNames, key) : null;

        var siteName = await db.Sites.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => s.Id == issue.SiteId).Select(s => s.Name).FirstOrDefaultAsync(ct);
        var departmentName = issue.DepartmentId is { } departmentId
            ? await db.Departments.IgnoreQueryFilters([QueryFilters.SoftDelete])
                .Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefaultAsync(ct)
            : null;
        var task = issue.SiteTaskId is { } taskId
            ? await db.SiteTasks.AsNoTracking().Where(t => t.Id == taskId)
                .Select(t => new { t.Title, t.DueDate }).FirstOrDefaultAsync(ct)
            : null;

        var timeline = events.Select(e => new IssueEventResponse(
            e.Id, e.Kind, e.From, e.To,
            e.Kind == IssueEventKind.Assigned ? MembershipName(e.From) : null,
            e.Kind == IssueEventKind.Assigned ? MembershipName(e.To) : null,
            e.Note, e.PhotoIds, e.OccurredAt, e.ActorId, NameOf(userNames, e.ActorId))).ToList();

        return new IssueDetailResponse(
            issue.Id, issue.Number, issue.Title, issue.Description, issue.SiteId, siteName,
            issue.DepartmentId, departmentName, issue.Priority, issue.Status, issue.Source,
            issue.AssigneeId is { } a ? new IssueAssigneeResponse(a, NameOf(membershipNames, a)) : null,
            issue.DueAt, issue.IsOverdue(now), issue.SiteTaskId, task?.Title, task?.DueDate, issue.FieldKey,
            issue.Resolution, issue.ResolvedAt, NameOf(userNames, issue.ResolvedById),
            issue.ClosedAt, NameOf(userNames, issue.ClosedById),
            issue.PhotoIds, issue.CreatedAt, timeline);
    }
}
