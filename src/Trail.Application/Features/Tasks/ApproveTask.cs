using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

public sealed record ApproveTaskCommand(string? Note = null);

public sealed class ApproveTaskHandler(IAppDbContext db, ISiteAccess access, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, ApproveTaskCommand? command, CancellationToken ct)
    {
        var note = command?.Note?.Trim().NullIfEmpty();
        if (note is { Length: > SiteTask.NoteMaxLength })
        {
            return Error.Validation("task.note_too_long", "The note is too long.",
                new Dictionary<string, string[]> { ["note"] = [$"Notes are limited to {SiteTask.NoteMaxLength} characters."] });
        }

        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        if (!task.IsAwaitingApproval)
            return Error.Conflict("task.not_awaiting_approval", "The task is not waiting for approval.");
        if (task.CompletedById is not null && task.CompletedById == user.UserId)
            return Error.Forbidden("task.self_approval", "You cannot approve a task you completed.");

        var now = clock.GetUtcNow().UtcDateTime;
        task.Approve(now, user.UserId, note);
        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}
