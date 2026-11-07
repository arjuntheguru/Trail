using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Notifications;

/// <summary>
/// Adds notification rows to the unit of work and routes each to email or not according to the recipient's
/// preferences. Never saves: event handlers run inside the save of the change that caused the notification, and
/// the jobs save after calling it.
/// </summary>
public sealed class NotificationWriter(IAppDbContext db)
{
    public async Task AddAsync(
        IEnumerable<Guid> membershipIds, NotificationKind kind, NotificationSubjectType subjectType, Guid subjectId,
        Guid? siteId, (string Title, string Body) text, CancellationToken ct)
    {
        // One notification of a kind per person per subject per save, however many handlers name them.
        // Rows not yet saved have no CreatedAt (the auditing interceptor stamps it on save).
        var pending = db.Notifications.Local
            .Where(n => n.CreatedAt == default && n.Kind == kind && n.SubjectId == subjectId)
            .Select(n => n.MembershipId)
            .ToHashSet();
        var ids = membershipIds.Distinct().Where(id => !pending.Contains(id)).ToList();
        if (ids.Count == 0) return;

        var preferences = await db.NotificationPreferences.AsNoTracking()
            .Where(p => ids.Contains(p.MembershipId))
            .ToDictionaryAsync(p => p.MembershipId, ct);

        foreach (var id in ids)
        {
            preferences.TryGetValue(id, out var preference);
            db.Notifications.Add(new Notification
            {
                MembershipId = id,
                Kind = kind,
                Title = IssuePolicy.Truncate(text.Title, Notification.TitleMaxLength),
                Body = IssuePolicy.Truncate(text.Body, Notification.BodyMaxLength),
                SubjectType = subjectType,
                SubjectId = subjectId,
                SiteId = siteId,
                EmailStatus = NotificationPreference.WantsEmail(preference, kind)
                    ? NotificationEmailStatus.Pending
                    : NotificationEmailStatus.Skipped,
            });
        }
    }

    public async Task<string?> SiteNameAsync(Guid siteId, CancellationToken ct) =>
        await db.Sites.AsNoTracking().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => s.Id == siteId).Select(s => s.Name).FirstOrDefaultAsync(ct);

    /// <summary>The tracked issue (no query when the change that raised the event is in the same unit of work).</summary>
    public async Task<Issue?> FindIssueAsync(Guid issueId, CancellationToken ct) => await db.Issues.FindAsync([issueId], ct);

    public async Task<SiteTask?> FindTaskAsync(Guid taskId, CancellationToken ct) => await db.SiteTasks.FindAsync([taskId], ct);
}
