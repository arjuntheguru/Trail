using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Issues;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>
/// One reading to add to a record log. <paramref name="Automated"/> marks readings from machines (sensors): they
/// cannot attach photos or write a corrective-action comment, so the "required on failure" and "requires photo"
/// rules are not applied; a failing reading is still recorded as a failure and still raises its issue.
/// </summary>
public sealed record RecordLogReading(
    IReadOnlyDictionary<string, FieldAnswer> Answers, string? Note, IReadOnlyList<Guid> PhotoIds,
    DateTime SubmittedAt, Guid? SubmittedById, bool Automated = false);

/// <summary>
/// The core of adding an entry to a record log, shared by people (<see cref="AddTaskEntryHandler"/>) and sensors:
/// checks the task, evaluates the answers, adds the entry, flags the task and raises issues for failures.
/// Adds to the unit of work; the caller saves.
/// </summary>
public sealed class RecordLogWriter(IAppDbContext db, IssueRaiser issues)
{
    /// <param name="now">The current time, used to decide whether the task can still be worked on.</param>
    public async Task<Result<TaskEntry>> AddAsync(SiteTask task, RecordLogReading reading, DateTime now, CancellationToken ct)
    {
        if (TaskRules.CheckKind(task, TaskKind.RecordLog) is { } wrongKind) return wrongKind;
        if (TaskRules.CheckWorkable(task, now) is { } notWorkable) return notWorkable;

        var evaluation = AnswerEvaluator.Evaluate(task.Fields, reading.Answers, requireAll: !reading.Automated);
        if (!evaluation.IsValid) return TaskRules.InvalidAnswers(evaluation.Problems);

        if (!reading.Automated)
        {
            if (task.RequiresPhoto && reading.PhotoIds.Count == 0) return TaskRules.PhotoRequired();
            if (await TaskRules.CheckAttachmentsAsync(db, task, reading.Answers, reading.PhotoIds, ct) is { } badAttachments)
                return badAttachments;
        }

        var entry = new TaskEntry
        {
            SiteTaskId = task.Id,
            Answers = new Dictionary<string, FieldAnswer>(reading.Answers),
            PhotoIds = [.. reading.PhotoIds],
            Note = reading.Note?.Trim().NullIfEmpty(),
            Failures = [.. evaluation.Failures],
            SubmittedAt = reading.SubmittedAt,
            SubmittedById = reading.SubmittedById,
        };
        db.TaskEntries.Add(entry);
        task.HasFailures |= evaluation.HasFailures;
        await issues.RaiseForFailuresAsync(task, entry, now, reading.SubmittedById, ct);
        return entry;
    }
}
