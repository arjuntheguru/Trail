using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Identity;
using Trail.Domain.Issues;
using Trail.Domain.Notifications;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Notifications;

/// <summary>
/// Sends the daily digest emails of the current tenant to everyone whose digest hour has arrived. The digest is
/// not an in-app notification, so it is sent straight from here. Each person's <see cref="NotificationPreference.LastDigestOn"/>
/// is saved before anything is sent: a crash then skips a digest rather than repeating it.
/// </summary>
public sealed class DailyDigestRunner(
    IAppDbContext db, IEmailSender email, INotificationLinks links, ITenantContext tenant, TimeProvider clock,
    ILogger<DailyDigestRunner> logger)
{
    /// <returns>The number of digests sent.</returns>
    public async Task<int> RunAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var organization = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == tenant.OrganizationId)
            .Select(o => new { o.Name, o.DefaultTimeZone })
            .FirstOrDefaultAsync(ct);
        if (organization is null) return 0;

        var sites = await db.Sites.AsNoTracking().Where(s => s.IsActive)
            .Select(s => new { s.Id, s.Name, s.RegionId, s.TimeZone })
            .ToListAsync(ct);

        var members = await db.Memberships.AsNoTracking()
            .Where(m => m.Status == MembershipStatus.Active && m.User.IsActive && m.User.EmailConfirmedAt != null)
            .Select(m => new Member(m.Id, m.AllSites, m.User.Email, m.User.FirstName, m.Role.Permissions))
            .ToListAsync(ct);
        if (members.Count == 0) return 0;

        var preferences = await db.NotificationPreferences.ToDictionaryAsync(p => p.MembershipId, ct);
        var directSites = (await db.MembershipSites.AsNoTracking().Select(x => new { x.MembershipId, x.SiteId }).ToListAsync(ct))
            .ToLookup(x => x.MembershipId, x => x.SiteId);
        var regions = (await db.MembershipRegions.AsNoTracking().Select(x => new { x.MembershipId, x.RegionId }).ToListAsync(ct))
            .ToLookup(x => x.MembershipId, x => x.RegionId);

        var due = new List<(Member Member, IReadOnlyDictionary<Guid, DigestSite> Sites, DateOnly Today)>();
        foreach (var member in members)
        {
            preferences.TryGetValue(member.MembershipId, out var preference);
            if (!(preference?.DigestEnabled ?? true)) continue;

            var reachable = member.AllSites
                ? sites
                : sites.Where(s => directSites[member.MembershipId].Contains(s.Id)
                                   || (s.RegionId is { } region && regions[member.MembershipId].Contains(region))).ToList();
            var zone = DigestSchedule.ZoneFor(reachable.Select(s => s.TimeZone), organization.DefaultTimeZone);
            var hour = preference?.DigestHour ?? NotificationPreference.DefaultDigestHour;
            if (!DigestSchedule.IsDue(now, zone, hour, preference?.LastDigestOn, out var today)) continue;

            // Recorded now, before any email goes out.
            if (preference is null)
            {
                preference = new NotificationPreference { MembershipId = member.MembershipId };
                db.NotificationPreferences.Add(preference);
            }
            preference.LastDigestOn = today;
            due.Add((member, reachable.ToDictionary(s => s.Id, s => new DigestSite(s.Id, s.Name, s.TimeZone)), today));
        }
        if (due.Count == 0) return 0;
        await db.SaveChangesAsync(ct);

        // Organization-wide rows are read once and filtered per person.
        var utcToday = DateOnly.FromDateTime(now);
        var missed = (await db.SiteTasks.AsNoTracking()
                .Where(t => t.Status == SiteTaskStatus.Missed && t.DueDate >= utcToday.AddDays(-3) && t.DueDate <= utcToday)
                .Select(t => new { t.Id, t.Title, t.SiteId, t.DueDate }).ToListAsync(ct))
            .Select(t => new DigestTaskRow(t.Id, t.Title, t.SiteId, t.DueDate)).ToList();
        var awaiting = (await db.SiteTasks.AsNoTracking()
                .Where(t => t.Status == SiteTaskStatus.Completed && t.RequiresApproval && t.ApprovedAt == null)
                .Select(t => new { t.Id, t.Title, t.SiteId, t.DueDate }).ToListAsync(ct))
            .Select(t => new DigestTaskRow(t.Id, t.Title, t.SiteId, t.DueDate)).ToList();
        var issues = (await db.Issues.AsNoTracking()
                .Where(i => i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress)
                .Select(i => new { i.Id, i.Number, i.Title, i.SiteId, i.AssigneeId, i.Priority, i.DueAt }).ToListAsync(ct))
            .Select(i => new DigestIssueRow(i.Id, i.Number, i.Title, i.SiteId, i.AssigneeId, i.Priority, i.DueAt)).ToList();

        var sent = 0;
        foreach (var (member, reachableSites, today) in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var content = DigestSelector.Select(member.MembershipId, member.Permissions, reachableSites, missed, awaiting, issues, now);
                var message = DigestComposer.Compose(member.Email, member.FirstName, organization.Name, today, content, links);
                if (message is null) continue;

                await email.SendAsync(message, ct);
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Not retried: the day is already recorded, and tomorrow's digest covers the same ground.
                logger.LogWarning(ex, "Daily digest for membership {MembershipId} failed.", member.MembershipId);
            }
        }
        return sent;
    }

    private sealed record Member(Guid MembershipId, bool AllSites, string Email, string FirstName, List<string> Permissions);
}
