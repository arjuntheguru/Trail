using Trail.Domain.Issues;

namespace Trail.Application.Features.Notifications;

/// <param name="NotifyOverdue">Send the first "this is overdue" notice.</param>
/// <param name="Escalate">Tell the next level up.</param>
public readonly record struct EscalationDecision(bool NotifyOverdue, bool Escalate)
{
    public static readonly EscalationDecision Nothing = new(false, false);
}

/// <summary>How wide a membership's access to a site is. Broader access means a higher level of management.</summary>
public enum ReachLevel
{
    /// <summary>Assigned to this site directly.</summary>
    Site,

    /// <summary>Assigned to the site's region.</summary>
    Region,

    /// <summary>Can see every site in the organization.</summary>
    AllSites,
}

public sealed record SiteMember(Guid MembershipId, Guid UserId, ReachLevel Level);

/// <summary>Decides when an overdue issue is announced and escalated, and who escalations go to. Pure.</summary>
public static class EscalationPolicy
{
    public static EscalationDecision Decide(
        IssueStatus status, IssuePriority priority, DateTime? dueAt, DateTime? overdueNotifiedAt, DateTime? escalatedAt, DateTime now)
    {
        if (status is not (IssueStatus.Open or IssueStatus.InProgress) || dueAt is not { } due || now <= due)
            return EscalationDecision.Nothing;

        // Escalate once the issue has been late for as long as it was originally given to be fixed.
        var escalate = escalatedAt is null && now - due > IssuePolicy.DefaultResponseTime(priority);
        return new EscalationDecision(overdueNotifiedAt is null, escalate);
    }

    /// <summary>Issues of <paramref name="priority"/> due before this are late enough to escalate at <paramref name="now"/>.</summary>
    public static DateTime EscalationCutoff(IssuePriority priority, DateTime now) => now - IssuePolicy.DefaultResponseTime(priority);

    /// <summary>The assignee when there is one, otherwise everyone who manages issues at the site.</summary>
    public static IReadOnlyList<Guid> OverdueRecipients(Guid? assigneeId, IReadOnlyList<SiteMember> managers) =>
        assigneeId is { } assignee ? [assignee] : managers.Select(m => m.MembershipId).Distinct().ToList();

    /// <summary>
    /// Managers above site level (region or organization-wide access); when there are none, all the site's managers.
    /// </summary>
    public static IReadOnlyList<Guid> EscalationRecipients(IReadOnlyList<SiteMember> managers)
    {
        var higher = managers.Where(m => m.Level >= ReachLevel.Region).Select(m => m.MembershipId).Distinct().ToList();
        return higher.Count > 0 ? higher : managers.Select(m => m.MembershipId).Distinct().ToList();
    }

    public static ReachLevel LevelOf(bool allSites, bool viaRegion) =>
        allSites ? ReachLevel.AllSites : viaRegion ? ReachLevel.Region : ReachLevel.Site;
}
