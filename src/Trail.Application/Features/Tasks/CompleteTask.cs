using FluentValidation;

using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Issues;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>
/// Checklist and form: the final answers, merged over the saved draft (the body wins per key). Record log: answers
/// are ignored, since readings are added as entries; only the note applies. <paramref name="RequestId"/> makes a
/// replay safe (a repeat returns the task without completing it again); <paramref name="CompletedAt"/> is when the
/// work was done, for completions made offline.
/// </summary>
public sealed record CompleteTaskCommand(
    IReadOnlyDictionary<string, FieldAnswer>? Answers = null, string? Note = null, IReadOnlyList<Guid>? PhotoIds = null,
    Guid? RequestId = null, DateTime? CompletedAt = null);
internal sealed class CompleteTaskValidator : AbstractValidator<CompleteTaskCommand>
{
    public CompleteTaskValidator() => TaskRules.AddCommonRules(this, x => x.Answers, x => x.Note, x => x.PhotoIds);
}

public sealed class CompleteTaskHandler(
    IAppDbContext db, ISiteAccess access, ICurrentUser user, IssueRaiser issues, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, CompleteTaskCommand command, CancellationToken ct)
    {
        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        var now = clock.GetUtcNow().UtcDateTime;

        // A replay never completes the task twice, whatever happened to it since.
        if (command.RequestId is { } requestId && task.CompletionRequestId == requestId)
            return await TaskMapping.ToDetailAsync(db, task, now, ct);

        var resolved = TaskRules.ResolveWorkTime(command.CompletedAt, now, "task.completed_at_out_of_range", "completedAt");
        if (resolved.IsFailure) return resolved.Error!;
        var at = resolved.Value;

        if (TaskRules.CheckWorkable(task, at) is { } notWorkable) return notWorkable;

        bool hasFailures;
        if (task.Kind == TaskKind.RecordLog)
        {
            // Each reading raised its own issues when it was added.
            var finished = FinishRecordLog(task, command);
            if (finished.IsFailure) return finished.Error!;
            hasFailures = finished.Value;
        }
        else
        {
            var submitted = await SubmitEntryAsync(task, command, at, ct);
            if (submitted.IsFailure) return submitted.Error!;
            hasFailures = submitted.Value.HasFailures;
            await issues.RaiseForFailuresAsync(task, submitted.Value, now, user.UserId, ct);
        }

        task.Complete(at, user.UserId, hasFailures);
        task.CompletionRequestId = command.RequestId ?? task.CompletionRequestId;
        task.SyncedAt = command.CompletedAt is null ? null : now;
        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }

    /// <returns>Whether any reading failed.</returns>
    private static Result<bool> FinishRecordLog(SiteTask task, CompleteTaskCommand command)
    {
        var entries = task.Entries.Where(e => e.SubmittedAt is not null).OrderBy(e => e.SubmittedAt).ToList();
        if (entries.Count == 0)
            return Error.Conflict("task.no_entries", "Record at least one entry before completing.");

        if (command.Note.NullIfEmpty() is { } note)
        {
            var last = entries[^1];
            var combined = last.Note is null ? note.Trim() : $"{last.Note}\n{note.Trim()}";
            if (combined.Length > SiteTask.NoteMaxLength)
            {
                return Error.Validation("task.note_too_long", "The note is too long.",
                    new Dictionary<string, string[]> { ["note"] = [$"Notes are limited to {SiteTask.NoteMaxLength} characters."] });
            }
            last.Note = combined;
        }

        return entries.Any(e => e.HasFailures);
    }

    /// <returns>The submitted entry.</returns>
    private async Task<Result<TaskEntry>> SubmitEntryAsync(SiteTask task, CompleteTaskCommand command, DateTime at, CancellationToken ct)
    {
        if (TaskRules.CheckKind(task, TaskKind.Checklist, TaskKind.Form) is { } wrongKind) return wrongKind;

        var entry = task.Entries.FirstOrDefault(e => e.SubmittedAt is null);
        var answers = new Dictionary<string, FieldAnswer>(entry?.Answers ?? []);
        foreach (var (key, answer) in command.Answers ?? new Dictionary<string, FieldAnswer>())
            answers[key] = answer;

        var evaluation = AnswerEvaluator.Evaluate(task.Fields, answers, requireAll: true);
        if (!evaluation.IsValid) return TaskRules.InvalidAnswers(evaluation.Problems);

        var photoIds = command.PhotoIds ?? entry?.PhotoIds ?? [];
        if (task.RequiresPhoto && photoIds.Count == 0) return TaskRules.PhotoRequired();
        if (await TaskRules.CheckAttachmentsAsync(db, task, answers, photoIds, ct) is { } badAttachments)
            return badAttachments;

        if (entry is null)
        {
            entry = new TaskEntry { SiteTaskId = task.Id };
            db.TaskEntries.Add(entry);
        }

        entry.Answers = answers;
        entry.Failures = [.. evaluation.Failures];
        entry.PhotoIds = [.. photoIds];
        if (command.Note is not null) entry.Note = command.Note.Trim().NullIfEmpty();
        entry.SubmittedAt = at;
        entry.SubmittedById = user.UserId;
        return entry;
    }
}