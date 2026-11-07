using FluentValidation;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Issues;

namespace Trail.Application.Features.Issues;

public sealed record CommentOnIssueCommand(string? Text, IReadOnlyList<Guid>? PhotoIds);

internal sealed class CommentOnIssueValidator : AbstractValidator<CommentOnIssueCommand>
{
    public CommentOnIssueValidator()
    {
        RuleFor(x => x.Text).MaximumLength(Issue.NoteMaxLength);
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Text) || x.PhotoIds is { Count: > 0 })
            .WithMessage("A comment needs text or a photo.");
    }
}

/// <summary>Comments are allowed in every state, so a closed issue can still collect notes.</summary>
public sealed class CommentOnIssueHandler(IAppDbContext db, ISiteAccess access, ICurrentUser caller, TimeProvider clock)
{
    public async Task<Result<IssueDetailResponse>> Handle(Guid id, CommentOnIssueCommand command, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var issue = await IssueRules.FindAsync(db, access, id, ct);
        if (issue is null) return IssueRules.NotFound;

        var photoIds = command.PhotoIds?.Distinct().ToList() ?? [];
        if (await IssueRules.ValidatePhotosAsync(db, photoIds, issue.SiteId, ct) is { } photoError) return photoError;

        issue.Comment(now, caller.UserId, command.Text ?? "", photoIds);
        await db.SaveChangesAsync(ct);
        return await IssueRules.LoadDetailAsync(db, id, now, ct);
    }
}
