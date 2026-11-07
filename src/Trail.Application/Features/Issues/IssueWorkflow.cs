using FluentValidation;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

public sealed record ResolveIssueCommand(string Resolution, IReadOnlyList<Guid>? PhotoIds);

public sealed record CloseIssueCommand(string? Note);

public sealed record ReopenIssueCommand(string Reason);

internal sealed class ResolveIssueValidator : AbstractValidator<ResolveIssueCommand>
{
    public ResolveIssueValidator() => RuleFor(x => x.Resolution).NotEmpty().MaximumLength(Issue.NoteMaxLength);
}

internal sealed class CloseIssueValidator : AbstractValidator<CloseIssueCommand>
{
    public CloseIssueValidator() => RuleFor(x => x.Note).MaximumLength(Issue.NoteMaxLength);
}

internal sealed class ReopenIssueValidator : AbstractValidator<ReopenIssueCommand>
{
    public ReopenIssueValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(Issue.NoteMaxLength);
}

/// <summary>Status transitions. Each checks the state first so the domain guards never throw.</summary>
public sealed class IssueWorkflowHandler(IAppDbContext db, ISiteAccess access, ICurrentUser caller, TimeProvider clock)
{
    public Task<Result<IssueDetailResponse>> Start(Guid id, CancellationToken ct) =>
        ApplyAsync(id, assigneeOrManager: true, ct, issue =>
        {
            if (issue.Status != IssueStatus.Open) return Task.FromResult<Error?>(IssueRules.NotOpen);
            issue.Start(Now, caller.UserId);
            return Task.FromResult<Error?>(null);
        });

    public Task<Result<IssueDetailResponse>> Resolve(Guid id, ResolveIssueCommand command, CancellationToken ct) =>
        ApplyAsync(id, assigneeOrManager: true, ct, async issue =>
        {
            if (!issue.IsActive) return IssueRules.NotActive;
            var photoIds = command.PhotoIds?.Distinct().ToList() ?? [];
            if (await IssueRules.ValidatePhotosAsync(db, photoIds, issue.SiteId, ct) is { } photoError) return photoError;
            issue.Resolve(Now, caller.UserId, command.Resolution.Trim(), photoIds);
            return null;
        });

    /// <summary>The note is optional, so the body may be missing; its length is checked here.</summary>
    public Task<Result<IssueDetailResponse>> Close(Guid id, CloseIssueCommand? command, CancellationToken ct) =>
        ApplyAsync(id, assigneeOrManager: false, ct, issue =>
        {
            if (command?.Note is { Length: > Issue.NoteMaxLength })
                return Task.FromResult<Error?>(Error.Validation("issue.note_too_long", "The note is too long."));
            if (issue.Status == IssueStatus.Closed) return Task.FromResult<Error?>(IssueRules.AlreadyClosed);
            issue.Close(Now, caller.UserId, command?.Note);
            return Task.FromResult<Error?>(null);
        });

    public Task<Result<IssueDetailResponse>> Reopen(Guid id, ReopenIssueCommand command, CancellationToken ct) =>
        ApplyAsync(id, assigneeOrManager: false, ct, issue =>
        {
            if (issue.IsActive) return Task.FromResult<Error?>(IssueRules.NotResolvedOrClosed);
            issue.Reopen(Now, caller.UserId, command.Reason.Trim());
            return Task.FromResult<Error?>(null);
        });

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private async Task<Result<IssueDetailResponse>> ApplyAsync(
        Guid id, bool assigneeOrManager, CancellationToken ct, Func<Issue, Task<Error?>> change)
    {
        var issue = await IssueRules.FindAsync(db, access, id, ct);
        if (issue is null) return IssueRules.NotFound;
        if (assigneeOrManager && !IssueRules.CanActOn(caller, issue)) return IssueRules.NotAssignee;

        if (await change(issue) is { } error) return error;
        await db.SaveChangesAsync(ct);
        return await IssueRules.LoadDetailAsync(db, id, Now, ct);
    }
}
