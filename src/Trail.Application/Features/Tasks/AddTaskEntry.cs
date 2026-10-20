using FluentValidation;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>One reading of a record log. Entries are final once added.</summary>
public sealed record AddTaskEntryCommand(
    IReadOnlyDictionary<string, FieldAnswer> Answers, string? Note = null, IReadOnlyList<Guid>? PhotoIds = null);

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
        var reading = new RecordLogReading(command.Answers, command.Note, command.PhotoIds ?? [], now, user.UserId);
        var added = await writer.AddAsync(task, reading, now, ct);
        if (added.IsFailure) return added.Error!;

        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}
