using FluentValidation;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

public sealed record ReopenTaskCommand(string Reason);

internal sealed class ReopenTaskValidator : AbstractValidator<ReopenTaskCommand>
{
    public ReopenTaskValidator() => RuleFor(x => x.Reason).NotEmpty().MaximumLength(SiteTask.NoteMaxLength);
}

/// <summary>Sends a completed task that has not been signed off back to the list, with the manager's reason.</summary>
public sealed class ReopenTaskHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, ReopenTaskCommand command, CancellationToken ct)
    {
        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        if (task.Status != SiteTaskStatus.Completed || task.ApprovedAt is not null)
            return Error.Conflict("task.cannot_reopen", "Only completed tasks that are not signed off can be reopened.");

        task.Reopen();
        task.ApprovalNote = command.Reason.Trim();

        // A checklist or form is edited again in its single entry; record log readings stay as recorded.
        if (task.Kind != TaskKind.RecordLog)
        {
            foreach (var entry in task.Entries)
            {
                entry.SubmittedAt = null;
                entry.SubmittedById = null;
            }
        }

        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, clock.GetUtcNow().UtcDateTime, ct);
    }
}
