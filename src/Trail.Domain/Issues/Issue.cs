using Trail.Domain.Common;

namespace Trail.Domain.Issues;

public enum IssueStatus
{
    /// <summary>Raised and waiting for someone to act.</summary>
    Open,

    /// <summary>Someone is working on the corrective action.</summary>
    InProgress,

    /// <summary>A corrective action was taken and described; waiting for a manager to verify it.</summary>
    Resolved,

    /// <summary>Verified and closed. Final unless a manager reopens it.</summary>
    Closed,
}

public enum IssuePriority
{
    Low,
    Medium,
    High,
    Critical,
}

public enum IssueSource
{
    /// <summary>Raised by a person, e.g. a broken fridge noticed during service.</summary>
    Manual,

    /// <summary>Raised automatically from a failing answer on a task.</summary>
    TaskFailure,
}

/// <summary>
/// A problem at a site that needs a corrective action: a failed check, a broken appliance, a pest sighting.
/// Moves Open → InProgress → Resolved (with the action taken) → Closed (verified). Every change is recorded
/// as an <see cref="IssueEvent"/> so the history can be shown to an inspector.
/// </summary>
public sealed class Issue : TenantEntity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 5000;
    public const int NoteMaxLength = 2000;
    public const int MaxPhotos = 20;

    /// <summary>Sequential per organization, shown to people as "#42".</summary>
    public int Number { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public Guid SiteId { get; set; }
    public Guid? DepartmentId { get; set; }
    public IssuePriority Priority { get; set; } = IssuePriority.Medium;
    public IssueStatus Status { get; private set; } = IssueStatus.Open;
    public IssueSource Source { get; set; } = IssueSource.Manual;

    /// <summary>The task, entry and field that failed, for issues raised from a task.</summary>
    public Guid? SiteTaskId { get; set; }
    public Guid? TaskEntryId { get; set; }
    public string? FieldKey { get; set; }

    /// <summary>Membership responsible for the corrective action.</summary>
    public Guid? AssigneeId { get; private set; }

    public DateTime? DueAt { get; private set; }
    public List<Guid> PhotoIds { get; set; } = [];

    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedById { get; private set; }

    /// <summary>The corrective action taken.</summary>
    public string? Resolution { get; private set; }

    public DateTime? ClosedAt { get; private set; }
    public Guid? ClosedById { get; private set; }

    public List<IssueEvent> Events { get; set; } = [];

    public bool IsActive => Status is IssueStatus.Open or IssueStatus.InProgress;

    public bool IsOverdue(DateTime now) => IsActive && DueAt is { } due && now > due;

    /// <summary>Records the "raised" event with the initial due time. Call once, after the issue's fields are set.</summary>
    public void Raised(DateTime now, Guid? userId, DateTime? dueAt)
    {
        if (Events.Count > 0) throw new InvalidOperationException("The issue was already raised.");
        DueAt = dueAt;
        Record(now, userId, IssueEventKind.Raised, to: Priority.ToString());
    }

    public void Comment(DateTime now, Guid? userId, string text, IReadOnlyList<Guid> photoIds)
    {
        if (string.IsNullOrWhiteSpace(text) && photoIds.Count == 0)
            throw new InvalidOperationException("A comment needs text or a photo.");
        Record(now, userId, IssueEventKind.Comment, note: text, photoIds: photoIds);
    }

    public void Assign(DateTime now, Guid? userId, Guid? assigneeId)
    {
        RequireActive("reassigned");
        if (assigneeId == AssigneeId) return;
        Record(now, userId, IssueEventKind.Assigned, AssigneeId?.ToString(), assigneeId?.ToString());
        AssigneeId = assigneeId;
    }

    public void ChangePriority(DateTime now, Guid? userId, IssuePriority priority)
    {
        RequireActive("reprioritised");
        if (priority == Priority) return;
        Record(now, userId, IssueEventKind.PriorityChanged, Priority.ToString(), priority.ToString());
        Priority = priority;
    }

    public void SetDue(DateTime now, Guid? userId, DateTime? dueAt)
    {
        RequireActive("rescheduled");
        if (dueAt == DueAt) return;
        Record(now, userId, IssueEventKind.DueChanged, DueAt?.ToString("O"), dueAt?.ToString("O"));
        DueAt = dueAt;
    }

    public void Start(DateTime now, Guid? userId)
    {
        if (Status != IssueStatus.Open) throw new InvalidOperationException($"A {Status} issue cannot be started.");
        ChangeStatus(now, userId, IssueStatus.InProgress, null);
    }

    public void Resolve(DateTime now, Guid? userId, string resolution, IReadOnlyList<Guid> photoIds)
    {
        RequireActive("resolved");
        if (string.IsNullOrWhiteSpace(resolution))
            throw new InvalidOperationException("Describe the corrective action taken.");
        ChangeStatus(now, userId, IssueStatus.Resolved, resolution, photoIds);
        Resolution = resolution;
        ResolvedAt = now;
        ResolvedById = userId;
    }

    /// <summary>Verifies a resolved issue. Managers may also close an active issue outright (e.g. a duplicate).</summary>
    public void Close(DateTime now, Guid? userId, string? note)
    {
        if (Status == IssueStatus.Closed) throw new InvalidOperationException("The issue is already closed.");
        ChangeStatus(now, userId, IssueStatus.Closed, note);
        ClosedAt = now;
        ClosedById = userId;
    }

    /// <summary>Sends a resolved or closed issue back, e.g. when the fix did not hold.</summary>
    public void Reopen(DateTime now, Guid? userId, string reason)
    {
        if (IsActive) throw new InvalidOperationException("The issue is not resolved or closed.");
        if (string.IsNullOrWhiteSpace(reason)) throw new InvalidOperationException("Give a reason for reopening.");
        ChangeStatus(now, userId, IssueStatus.Open, reason);
        Resolution = null;
        ResolvedAt = null;
        ResolvedById = null;
        ClosedAt = null;
        ClosedById = null;
    }

    private void RequireActive(string action)
    {
        if (!IsActive) throw new InvalidOperationException($"A {Status} issue cannot be {action}.");
    }

    private void ChangeStatus(DateTime now, Guid? userId, IssueStatus status, string? note, IReadOnlyList<Guid>? photoIds = null)
    {
        Record(now, userId, IssueEventKind.StatusChanged, Status.ToString(), status.ToString(), note, photoIds);
        Status = status;
    }

    private void Record(
        DateTime now, Guid? userId, IssueEventKind kind,
        string? from = null, string? to = null, string? note = null, IReadOnlyList<Guid>? photoIds = null) =>
        Events.Add(new IssueEvent
        {
            IssueId = Id,
            Kind = kind,
            From = from,
            To = to,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PhotoIds = photoIds is null ? [] : [.. photoIds],
            OccurredAt = now,
            ActorId = userId,
        });
}

public enum IssueEventKind
{
    Raised,
    Comment,
    StatusChanged,
    Assigned,
    PriorityChanged,
    DueChanged,
}

/// <summary>One entry in an issue's timeline. Never edited or deleted.</summary>
public sealed class IssueEvent : TenantEntity
{
    public Guid IssueId { get; set; }
    public IssueEventKind Kind { get; set; }

    /// <summary>Previous and new value for changes (status, priority, assignee membership id, due time).</summary>
    public string? From { get; set; }
    public string? To { get; set; }

    public string? Note { get; set; }
    public List<Guid> PhotoIds { get; set; } = [];
    public DateTime OccurredAt { get; set; }
    public Guid? ActorId { get; set; }
}
