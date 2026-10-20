using FluentValidation;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>A draft: <see cref="Answers"/> replace whatever was saved before, so clients send the full draft.</summary>
public sealed record SaveTaskProgressCommand(
    IReadOnlyDictionary<string, FieldAnswer> Answers, string? Note = null, IReadOnlyList<Guid>? PhotoIds = null);

internal sealed class SaveTaskProgressValidator : AbstractValidator<SaveTaskProgressCommand>
{
    public SaveTaskProgressValidator()
    {
        RuleFor(x => x.Answers).NotNull();
        TaskRules.AddCommonRules(this, x => x.Answers, x => x.Note, x => x.PhotoIds);
    }
}

public sealed class SaveTaskProgressHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, SaveTaskProgressCommand command, CancellationToken ct)
    {
        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        var now = clock.GetUtcNow().UtcDateTime;
        if (TaskRules.CheckKind(task, TaskKind.Checklist, TaskKind.Form) is { } wrongKind) return wrongKind;
        if (TaskRules.CheckWorkable(task, now) is { } notWorkable) return notWorkable;

        var evaluation = AnswerEvaluator.Evaluate(task.Fields, command.Answers, requireAll: false);
        if (!evaluation.IsValid) return TaskRules.InvalidAnswers(evaluation.Problems);

        var draft = task.Entries.FirstOrDefault(e => e.SubmittedAt is null);
        var photoIds = command.PhotoIds ?? draft?.PhotoIds ?? [];
        if (await TaskRules.CheckAttachmentsAsync(db, task, command.Answers, photoIds, ct) is { } badAttachments)
            return badAttachments;

        if (draft is null)
        {
            draft = new TaskEntry { SiteTaskId = task.Id };
            db.TaskEntries.Add(draft); // also joins task.Entries through relationship fix-up
        }

        draft.Answers = new Dictionary<string, FieldAnswer>(command.Answers);
        draft.Failures = [.. evaluation.Failures];
        draft.PhotoIds = [.. photoIds];
        if (command.Note is not null) draft.Note = command.Note.Trim().NullIfEmpty();

        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}
