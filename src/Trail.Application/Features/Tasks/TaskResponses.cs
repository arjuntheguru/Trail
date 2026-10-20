using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>One line of a task list. <see cref="CanCompleteLate"/> marks missed tasks that can still be done.</summary>
public sealed record TaskListItemResponse(
    Guid Id,
    Guid SiteId,
    string SiteName,
    string Title,
    TaskKind Kind,
    SiteTaskStatus Status,
    DateOnly DueDate,
    DateTime DueAt,
    DateTime? AvailableAt,
    bool IsOverdue,
    bool IsAwaitingApproval,
    bool RequiresApproval,
    bool RequiresPhoto,
    Guid? DepartmentId,
    SiteTaskSource Source,
    int EntryCount,
    DateTime? CompletedAt,
    string? CompletedBy,
    bool CompletedLate,
    bool HasFailures,
    bool CanCompleteLate);

/// <summary>A site's list for one local day, with the date and zone it was computed for.</summary>
public sealed record SiteTaskListResponse(
    Guid SiteId, string SiteName, DateOnly Date, string TimeZone, IReadOnlyList<TaskListItemResponse> Items);

public sealed record TaskEntryResponse(
    Guid Id,
    IReadOnlyDictionary<string, FieldAnswer> Answers,
    IReadOnlyList<Guid> PhotoIds,
    string? Note,
    IReadOnlyList<FieldFailure> Failures,
    bool HasFailures,
    DateTime? SubmittedAt,
    Guid? SubmittedById,
    string? SubmittedBy);

public sealed record TaskDetailResponse(
    Guid Id,
    Guid SiteId,
    string SiteName,
    string TimeZone,
    string Title,
    string? Description,
    TaskKind Kind,
    SiteTaskStatus Status,
    SiteTaskSource Source,
    IReadOnlyList<TemplateField> Fields,
    bool RequiresApproval,
    bool RequiresPhoto,
    Guid? DepartmentId,
    DateOnly DueDate,
    DateTime DueAt,
    DateTime? AvailableAt,
    bool IsOverdue,
    bool IsAwaitingApproval,
    bool CanCompleteLate,
    int SnoozeCount,
    DateTime? CompletedAt,
    Guid? CompletedById,
    string? CompletedBy,
    bool CompletedLate,
    bool HasFailures,
    DateTime? ApprovedAt,
    Guid? ApprovedById,
    string? ApprovedBy,
    string? ApprovalNote,
    IReadOnlyList<TaskEntryResponse> Entries);
