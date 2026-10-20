using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Identity;
using Trail.Domain.Notifications;

namespace Trail.Application.Features.Notifications;

/// <summary>
/// Sends the pending notification emails of the current tenant. Safe to run on several instances at once: a batch is
/// claimed with a conditional bulk update (Pending to Sending, stamped with a claim token), and only the run whose
/// update changed a row loads and sends it.
/// </summary>
public sealed class NotificationEmailRunner(
    IAppDbContext db, IEmailSender email, INotificationLinks links, TimeProvider clock, ILogger<NotificationEmailRunner> logger)
{
    public const int BatchSize = 50;

    /// <summary>A claimed row not finished after this long belongs to a run that died; it goes back to Pending.</summary>
    public static readonly TimeSpan ClaimTimeout = TimeSpan.FromMinutes(10);

    /// <returns>The number of emails sent.</returns>
    public async Task<int> SendPendingAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await ReleaseAbandonedClaimsAsync(now, ct);

        var token = Guid.NewGuid();
        if (await ClaimAsync(token, now, ct) == 0) return 0;

        var claimed = await db.Notifications
            .Where(n => n.EmailClaimToken == token && n.EmailStatus == NotificationEmailStatus.Sending)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(ct);

        var membershipIds = claimed.Select(n => n.MembershipId).Distinct().ToList();
        var recipients = await db.Memberships.AsNoTracking()
            .Where(m => membershipIds.Contains(m.Id))
            .Select(m => new Recipient(
                m.Id, m.Status == MembershipStatus.Active && m.User.IsActive && m.User.EmailConfirmedAt != null,
                m.User.Email, m.User.FirstName))
            .ToDictionaryAsync(r => r.MembershipId, ct);

        var sent = 0;
        foreach (var notification in claimed)
        {
            ct.ThrowIfCancellationRequested();
            notification.EmailClaimToken = null;

            if (!recipients.TryGetValue(notification.MembershipId, out var recipient) || !recipient.CanReceiveEmail)
            {
                notification.EmailStatus = NotificationEmailStatus.Skipped;
                await db.SaveChangesAsync(ct);
                continue;
            }

            try
            {
                await email.SendAsync(NotificationEmailComposer.Compose(notification, recipient.Email, recipient.FirstName, links), ct);
                notification.EmailStatus = NotificationEmailStatus.Sent;
                notification.EmailSentAt = clock.GetUtcNow().UtcDateTime;
                sent++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notification.EmailAttempts++;
                notification.EmailStatus = EmailBackoff.ShouldGiveUp(notification.EmailAttempts)
                    ? NotificationEmailStatus.Failed
                    : NotificationEmailStatus.Pending;
                logger.LogWarning(ex, "Notification email {NotificationId} failed (attempt {Attempts}).", notification.Id, notification.EmailAttempts);
            }

            // Saved one by one so a crash later in the batch cannot make an already sent email go out again.
            // The save stamps UpdatedAt, which is what the next retry is measured from.
            await db.SaveChangesAsync(ct);
        }
        return sent;
    }

    private async Task ReleaseAbandonedClaimsAsync(DateTime now, CancellationToken ct)
    {
        var cutoff = now - ClaimTimeout;
        await db.Notifications
            .Where(n => n.EmailStatus == NotificationEmailStatus.Sending && n.UpdatedAt < cutoff)
            .ExecuteUpdateAsync(u => u
                .SetProperty(n => n.EmailStatus, NotificationEmailStatus.Pending)
                .SetProperty(n => n.EmailClaimToken, (Guid?)null)
                .SetProperty(n => n.UpdatedAt, now)
                .SetProperty(n => n.ConcurrencyStamp, Guid.NewGuid()), ct);
    }

    /// <returns>The number of rows this run now owns.</returns>
    private async Task<int> ClaimAsync(Guid token, DateTime now, CancellationToken ct)
    {
        // Only rows past their minimum backoff are worth looking at; the exact per-attempt wait is applied below.
        var retryCutoff = now - EmailBackoff.MinimumDelay;
        var candidates = await db.Notifications.AsNoTracking()
            .Where(n => n.EmailStatus == NotificationEmailStatus.Pending
                        && (n.EmailAttempts == 0 || n.UpdatedAt <= retryCutoff))
            .OrderBy(n => n.CreatedAt)
            .Take(BatchSize)
            .Select(n => new { n.Id, n.EmailAttempts, n.UpdatedAt })
            .ToListAsync(ct);

        var due = candidates.Where(c => EmailBackoff.IsDue(c.EmailAttempts, c.UpdatedAt, now)).Select(c => c.Id).ToList();
        if (due.Count == 0) return 0;

        // The Pending condition is what makes this a claim: a row another instance took meanwhile is not Pending.
        return await db.Notifications
            .Where(n => due.Contains(n.Id) && n.EmailStatus == NotificationEmailStatus.Pending)
            .ExecuteUpdateAsync(u => u
                .SetProperty(n => n.EmailStatus, NotificationEmailStatus.Sending)
                .SetProperty(n => n.EmailClaimToken, (Guid?)token)
                .SetProperty(n => n.UpdatedAt, now)
                .SetProperty(n => n.ConcurrencyStamp, Guid.NewGuid()), ct);
    }

    private sealed record Recipient(Guid MembershipId, bool CanReceiveEmail, string Email, string FirstName);
}
