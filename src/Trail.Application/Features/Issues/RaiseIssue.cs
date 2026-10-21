using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Issues;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Issues;

public sealed record RaiseIssueCommand(
    string Title,
    string? Description,
    Guid SiteId,
    Guid? DepartmentId,
    IssuePriority? Priority,
    DateTime? DueAt,
    Guid? AssigneeId,
    IReadOnlyList<Guid>? PhotoIds);

internal sealed class RaiseIssueValidator : AbstractValidator<RaiseIssueCommand>
{
    public RaiseIssueValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(Issue.TitleMaxLength);
        RuleFor(x => x.Description).MaximumLength(Issue.DescriptionMaxLength);
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.Priority!.Value).IsInEnum().When(x => x.Priority is not null);
    }
}

public sealed class RaiseIssueHandler(
    IAppDbContext db, ISiteAccess access, ICurrentUser caller, ISequenceAllocator sequences, TimeProvider clock)
{
    public async Task<Result<IssueDetailResponse>> Handle(RaiseIssueCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;

        // A site the caller cannot reach is reported like one that does not exist.
        var site = await db.Sites.AsNoTracking().Where(s => s.Id == command.SiteId).FirstOrDefaultAsync(ct);
        if (site is null || !await access.CanAccessSiteAsync(site.Id, ct))
            return Error.Validation("issue.unknown_site", "The site does not exist.");
        if (!site.IsActive) return Error.Validation("issue.site_inactive", "The site is not active.");

        if (command.DepartmentId is { } departmentId && !await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            return Error.Validation("issue.unknown_department", "The department does not exist.");

        var priority = command.Priority ?? IssuePriority.Medium;
        DateTime? dueAt;
        if (command.DueAt is { } requested)
        {
            dueAt = IssueRules.AsUtc(requested);
            if (dueAt <= now) return IssueRules.DueInPast;
        }
        else
        {
            dueAt = now + IssuePolicy.DefaultResponseTime(priority);
        }

        var photoIds = command.PhotoIds?.Distinct().ToList() ?? [];
        if (await IssueRules.ValidatePhotosAsync(db, photoIds, site.Id, ct) is { } photoError) return photoError;
        if (command.AssigneeId is { } assigneeId
            && await IssueRules.ValidateAssigneeAsync(db, assigneeId, site.Id, ct) is { } assigneeError)
            return assigneeError;

        var issue = new Issue
        {
            Title = command.Title.Trim(),
            Description = command.Description?.Trim().NullIfEmpty(),
            SiteId = site.Id,
            DepartmentId = command.DepartmentId,
            Priority = priority,
            Source = IssueSource.Manual,
            PhotoIds = photoIds,
        };
        issue.Raised(now, caller.UserId, dueAt);
        // Event ids are random within a millisecond, so give the assignment a later timestamp to keep the
        // timeline order (OccurredAt, then Id) deterministic: raised first, then assigned.
        if (command.AssigneeId is { } assignee) issue.Assign(now.AddMicroseconds(1), caller.UserId, assignee);
        issue.Number = checked((int)await sequences.NextAsync(OrganizationSequence.Issues, 1, ct));

        db.Issues.Add(issue);
        await db.SaveChangesAsync(ct);
        return await IssueRules.LoadDetailAsync(db, issue.Id, now, ct);
    }
}
