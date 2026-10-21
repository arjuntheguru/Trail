using Trail.Domain.Issues;

namespace Trail.Application.Features.Notifications;

public enum StatusChangeAudience
{
    None,

    /// <summary>People who hold issues.manage at the site, e.g. to verify a resolution.</summary>
    Managers,

    /// <summary>Whoever the issue is assigned to.</summary>
    Assignee,
}

/// <summary>The decisions about who is told what. Pure, so the rules can be tested without a database.</summary>
public static class NotificationPolicy
{
    /// <summary>
    /// A new issue always goes to the site's managers when it is urgent or came from a failed check. A routine
    /// manual issue only does when nobody has been assigned: an assignee has already been told and owns it.
    /// </summary>
    public static bool NotifyManagersOfRaise(IssuePriority priority, IssueSource source, bool hasAssignee) =>
        priority >= IssuePriority.High || source != IssueSource.Manual || !hasAssignee;

    public static StatusChangeAudience AudienceForStatusChange(IssueStatus from, IssueStatus to) => to switch
    {
        IssueStatus.Resolved => StatusChangeAudience.Managers,
        IssueStatus.Closed => StatusChangeAudience.Assignee,
        IssueStatus.Open when from is IssueStatus.Resolved or IssueStatus.Closed => StatusChangeAudience.Assignee,
        _ => StatusChangeAudience.None,
    };

    public static string StatusChangeVerb(IssueStatus from, IssueStatus to) => to switch
    {
        IssueStatus.Resolved => "resolved",
        IssueStatus.Closed => "closed",
        IssueStatus.Open when from is IssueStatus.Resolved or IssueStatus.Closed => "reopened",
        IssueStatus.InProgress => "started",
        _ => to.ToString().ToLowerInvariant(),
    };

    /// <summary>The assignee, or the raiser when nobody is assigned. Null when there is no one to tell.</summary>
    public static Guid? CommentRecipient(Guid? assigneeId, Guid? raiserMembershipId) => assigneeId ?? raiserMembershipId;

    /// <summary>Distinct candidates minus anyone excluded (the actor, people another handler already covers).</summary>
    public static IReadOnlyList<Guid> Recipients(IEnumerable<Guid> candidates, params Guid?[] excluded)
    {
        var skip = excluded.OfType<Guid>().ToHashSet();
        return candidates.Where(id => !skip.Contains(id)).Distinct().ToList();
    }
}
