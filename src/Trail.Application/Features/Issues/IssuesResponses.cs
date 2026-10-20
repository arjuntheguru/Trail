using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

public sealed record IssueAssigneeResponse(Guid MembershipId, string? Name);

public sealed record IssueListItemResponse(
    Guid Id,
    int Number,
    string Title,
    Guid SiteId,
    string? SiteName,
    IssuePriority Priority,
    IssueStatus Status,
    IssueSource Source,
    IssueAssigneeResponse? Assignee,
    DateTime? DueAt,
    bool IsOverdue,
    DateTime CreatedAt,
    int PhotoCount);

public sealed record IssueSummaryResponse(
    IReadOnlyDictionary<IssueStatus, int> ByStatus,
    int Overdue,
    IReadOnlyDictionary<IssuePriority, int> ActiveByPriority,
    int AssignedToMe);

public sealed record IssueEventResponse(
    Guid Id,
    IssueEventKind Kind,
    string? From,
    string? To,
    string? FromName,
    string? ToName,
    string? Note,
    IReadOnlyList<Guid> PhotoIds,
    DateTime OccurredAt,
    Guid? ActorId,
    string? ActorName);

public sealed record IssueDetailResponse(
    Guid Id,
    int Number,
    string Title,
    string? Description,
    Guid SiteId,
    string? SiteName,
    Guid? DepartmentId,
    string? DepartmentName,
    IssuePriority Priority,
    IssueStatus Status,
    IssueSource Source,
    IssueAssigneeResponse? Assignee,
    DateTime? DueAt,
    bool IsOverdue,
    Guid? SiteTaskId,
    string? TaskTitle,
    DateOnly? TaskDueDate,
    string? FieldKey,
    string? Resolution,
    DateTime? ResolvedAt,
    string? ResolvedBy,
    DateTime? ClosedAt,
    string? ClosedBy,
    IReadOnlyList<Guid> PhotoIds,
    DateTime CreatedAt,
    IReadOnlyList<IssueEventResponse> Timeline);
