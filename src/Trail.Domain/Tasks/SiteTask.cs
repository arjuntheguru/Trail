using Trail.Domain.Common;

namespace Trail.Domain.Tasks;

public enum SiteTaskStatus
{
    /// <summary>On the list and not yet done. Past <see cref="SiteTask.DueAt"/> it is overdue but can still be done.</summary>
    Open,

    /// <summary>Done; awaiting sign-off when the template requires approval.</summary>
    Completed,

    /// <summary>The site's day ended without it being done.</summary>
    Missed,

    /// <summary>Withdrawn, e.g. its schedule was removed before it fell due.</summary>
    Cancelled,
}

public enum SiteTaskSource
{
    Scheduled,
    AdHoc,
}

/// <summary>
/// One task on one site's list for one local day. Holds a snapshot of the template's fields so later template
/// edits do not change what was asked. Scheduled tasks are unique per (schedule, site, scheduled date).
/// </summary>
public sealed class SiteTask : TenantEntity
{
    public const int NoteMaxLength = 2000;
    public const int MaxSnoozeDays = 14;

    /// <summary>How long after its due time a missed task can still be completed (marked late).</summary>
    public static readonly TimeSpan LateCompletionWindow = TimeSpan.FromDays(7);

    public Guid SiteId { get; set; }
    public Guid TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public Guid? ScheduleId { get; set; }
    public Guid? DepartmentId { get; set; }
    public SiteTaskSource Source { get; set; }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public TaskKind Kind { get; set; }
    public List<TemplateField> Fields { get; set; } = [];
    public bool RequiresApproval { get; set; }
    public bool RequiresPhoto { get; set; }

    /// <summary>The site-local day the schedule produced this task for. Never changes, even when snoozed.</summary>
    public DateOnly ScheduledDate { get; set; }

    /// <summary>The site-local day the task is currently listed on.</summary>
    public DateOnly DueDate { get; set; }

    public DateTime DueAt { get; set; }
    public DateTime? AvailableAt { get; set; }

    public SiteTaskStatus Status { get; set; } = SiteTaskStatus.Open;
    public int SnoozeCount { get; set; }

    public DateTime? CompletedAt { get; set; }
    public Guid? CompletedById { get; set; }
    public bool CompletedLate { get; set; }
    public bool HasFailures { get; set; }

    public DateTime? ApprovedAt { get; set; }
    public Guid? ApprovedById { get; set; }
    public string? ApprovalNote { get; set; }

    public List<TaskEntry> Entries { get; set; } = [];

    /// <summary>
    /// A task for <paramref name="date"/> at a site, copying the template's current fields. Times are site-local:
    /// due at <paramref name="dueTime"/> and listed from <paramref name="availableFrom"/> (null: all day).
    /// </summary>
    public static SiteTask FromTemplate(
        TaskTemplate template, Guid siteId, TimeZoneInfo siteZone, DateOnly date, TimeOnly dueTime, TimeOnly? availableFrom,
        SiteTaskSource source, Guid? scheduleId = null, Guid? departmentId = null) => new()
    {
        SiteId = siteId,
        TemplateId = template.Id,
        TemplateVersion = template.Version,
        ScheduleId = scheduleId,
        DepartmentId = departmentId,
        Source = source,
        Title = template.Name,
        Description = template.Description,
        Kind = template.Kind,
        Fields = [.. template.Fields],
        RequiresApproval = template.RequiresApproval,
        RequiresPhoto = template.RequiresPhoto,
        ScheduledDate = date,
        DueDate = date,
        DueAt = SiteTime.ToUtc(date, dueTime, siteZone),
        AvailableAt = availableFrom is { } from ? SiteTime.ToUtc(date, from, siteZone) : null,
    };

    public bool IsOverdue(DateTime now) => Status == SiteTaskStatus.Open && now > DueAt;

    public bool IsAwaitingApproval => Status == SiteTaskStatus.Completed && RequiresApproval && ApprovedAt is null;

    /// <summary>Open, or missed but still within the late-completion window.</summary>
    public bool CanBeWorkedOn(DateTime now) =>
        Status == SiteTaskStatus.Open || (Status == SiteTaskStatus.Missed && now <= DueAt + LateCompletionWindow);

    public void Complete(DateTime now, Guid? userId, bool hasFailures)
    {
        if (!CanBeWorkedOn(now)) throw new InvalidOperationException($"A {Status} task cannot be completed.");
        CompletedLate = now > DueAt || Status == SiteTaskStatus.Missed;
        Status = SiteTaskStatus.Completed;
        CompletedAt = now;
        CompletedById = userId;
        HasFailures = hasFailures;
        Raise(new TaskCompleted(Id, SiteId, userId, now));
    }

    public void Approve(DateTime now, Guid? userId, string? note)
    {
        if (!IsAwaitingApproval) throw new InvalidOperationException("Only completed tasks awaiting sign-off can be approved.");
        ApprovedAt = now;
        ApprovedById = userId;
        ApprovalNote = note;
        Raise(new TaskApproved(Id, SiteId, userId, now));
    }

    /// <summary>Sends a completed task back to the list, e.g. when a manager rejects the sign-off.</summary>
    public void Reopen()
    {
        if (Status != SiteTaskStatus.Completed || ApprovedAt is not null)
            throw new InvalidOperationException("Only completed tasks that are not signed off can be reopened.");
        Status = SiteTaskStatus.Open;
        CompletedAt = null;
        CompletedById = null;
        CompletedLate = false;
    }

    /// <summary>Moves an open task to a later local day, shifting its due time with it.</summary>
    public void Snooze(DateOnly newDate, TimeZoneInfo siteZone)
    {
        if (Status != SiteTaskStatus.Open) throw new InvalidOperationException("Only open tasks can be snoozed.");
        if (newDate <= DueDate) throw new InvalidOperationException("Snooze to a later day.");

        var localDue = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DueAt, siteZone));
        DueAt = SiteTime.ToUtc(newDate, localDue, siteZone);
        if (AvailableAt is { } available)
        {
            var localAvailable = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(available, siteZone));
            AvailableAt = SiteTime.ToUtc(newDate, localAvailable, siteZone);
        }
        DueDate = newDate;
        SnoozeCount++;
    }
}
