using FluentValidation;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

public sealed record AssignIssueCommand(Guid? AssigneeId);

public sealed record ChangeIssuePriorityCommand(IssuePriority Priority);

public sealed record SetIssueDueCommand(DateTime? DueAt);

// Nothing to check beyond the shape; these exist so the endpoint filter has a validator to resolve.
internal sealed class AssignIssueValidator : AbstractValidator<AssignIssueCommand>;

internal sealed class SetIssueDueValidator : AbstractValidator<SetIssueDueCommand>;

internal sealed class ChangeIssuePriorityValidator : AbstractValidator<ChangeIssuePriorityCommand>
{
    public ChangeIssuePriorityValidator() => RuleFor(x => x.Priority).IsInEnum();
}

/// <summary>Assignee, priority and due time changes. Only active issues can be changed.</summary>
public sealed class ChangeIssueHandler(IAppDbContext db, ISiteAccess access, ICurrentUser caller, TimeProvider clock)
{
    public Task<Result<IssueDetailResponse>> Assign(Guid id, AssignIssueCommand command, CancellationToken ct) =>
        ApplyAsync(id, ct, async (issue, now) =>
        {
            if (command.AssigneeId is { } assigneeId
                && await IssueRules.ValidateAssigneeAsync(db, assigneeId, issue.SiteId, ct) is { } error)
                return error;
            issue.Assign(now, caller.UserId, command.AssigneeId);
            return null;
        });

    public Task<Result<IssueDetailResponse>> ChangePriority(Guid id, ChangeIssuePriorityCommand command, CancellationToken ct) =>
        ApplyAsync(id, ct, (issue, now) =>
        {
            issue.ChangePriority(now, caller.UserId, command.Priority);
            return Task.FromResult<Error?>(null);
        });

    public Task<Result<IssueDetailResponse>> SetDue(Guid id, SetIssueDueCommand command, CancellationToken ct) =>
        ApplyAsync(id, ct, (issue, now) =>
        {
            DateTime? dueAt = command.DueAt is { } requested ? IssueRules.AsUtc(requested) : null;
            if (dueAt <= now) return Task.FromResult<Error?>(IssueRules.DueInPast);
            issue.SetDue(now, caller.UserId, dueAt);
            return Task.FromResult<Error?>(null);
        });

    private async Task<Result<IssueDetailResponse>> ApplyAsync(
        Guid id, CancellationToken ct, Func<Issue, DateTime, Task<Error?>> change)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var issue = await IssueRules.FindAsync(db, access, id, ct);
        if (issue is null) return IssueRules.NotFound;
        if (!issue.IsActive) return IssueRules.NotActive;

        if (await change(issue, now) is { } error) return error;
        await db.SaveChangesAsync(ct);
        return await IssueRules.LoadDetailAsync(db, id, now, ct);
    }
}
