using FluentValidation;
using Trail.Application.Common;
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

public sealed class AddTaskEntryHandler(IAppDbContext db, ISiteAccess access, ICurrentUser user, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, AddTaskEntryCommand command, CancellationToken ct)
    {
        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        var now = clock.GetUtcNow().UtcDateTime;
        if (TaskRules.CheckKind(task, TaskKind.RecordLog) is { } wrongKind) return wrongKind;
        if (TaskRules.CheckWorkable(task, now) is { } notWorkable) return notWorkable;

        var evaluation = AnswerEvaluator.Evaluate(task.Fields, command.Answers, requireAll: true);
        if (!evaluation.IsValid) return TaskRules.InvalidAnswers(evaluation.Problems);

        var photoIds = command.PhotoIds ?? [];
        if (task.RequiresPhoto && photoIds.Count == 0) return TaskRules.PhotoRequired();
        if (await TaskRules.CheckAttachmentsAsync(db, task, command.Answers, photoIds, ct) is { } badAttachments)
            return badAttachments;

        db.TaskEntries.Add(new TaskEntry
        {
            SiteTaskId = task.Id,
            Answers = new Dictionary<string, FieldAnswer>(command.Answers),
            PhotoIds = [.. photoIds],
            Note = command.Note?.Trim().NullIfEmpty(),
            Failures = [.. evaluation.Failures],
            SubmittedAt = now,
            SubmittedById = user.UserId,
        });
        task.HasFailures |= evaluation.HasFailures;

        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}
