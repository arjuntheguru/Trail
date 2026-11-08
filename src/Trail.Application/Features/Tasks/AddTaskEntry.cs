using FluentValidation;

using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>One reading of a record log. Entries are final once added.</summary>
public sealed record AddTaskEntryCommand(
    IReadOnlyDictionary<string, FieldAnswer> Answers, string? Note = null, IReadOnlyList<Guid>? PhotoIds = null,
    Guid? RequestId = null, DateTime? RecordedAt = null);

internal sealed class AddTaskEntryValidator : AbstractValidator<AddTaskEntryCommand>
{
    public AddTaskEntryValidator()
    {
        RuleFor(x => x.Answers).NotNull();
        TaskRules.AddCommonRules(this, x => x.Answers, x => x.Note, x => x.PhotoIds);
    }
}

public sealed class AddTaskEntryHandler(
    IAppDbContext db, ISiteAccess access, ICurrentUser user, RecordLogWriter writer, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, AddTaskEntryCommand command, CancellationToken ct)
    {
        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        var now = clock.GetUtcNow().UtcDateTime;
        if (command.RequestId is { } requestId && task.Entries.Any(e => e.RequestId == requestId))
            return await TaskMapping.ToDetailAsync(db, task, now, ct);

        var resolved = TaskRules.ResolveWorkTime(command.RecordedAt, now, "task.recorded_at_out_of_range", "recordedAt");
        if (resolved.IsFailure) return resolved.Error!;
        var at = resolved.Value;

        var reading = new RecordLogReading(command.Answers, command.Note, command.PhotoIds ?? [], at, user.UserId)
        {
            RequestId = command.RequestId,
            SyncedAt = command.RecordedAt is null ? null : now,
            WorkableAt = at,
        };
        var added = await writer.AddAsync(task, reading, now, ct);
        if (added.IsFailure) return added.Error!;

        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}