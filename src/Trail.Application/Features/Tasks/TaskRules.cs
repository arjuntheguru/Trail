using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Files;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>Checks and lookups shared by the use cases that work on a task. Domain methods throw on bad state, so check first.</summary>
internal static class TaskRules
{
    public const int MaxAnswers = 200;
    public const int MaxTaskPhotos = 20;

    public static Error NotFound { get; } = Error.NotFound("task.not_found", "Task not found.");

    /// <summary>
    /// Loads a tracked task with its entries, or null when it does not exist or its site is outside the caller's
    /// scope. Both look the same so ids cannot be probed.
    /// </summary>
    public static async Task<SiteTask?> FindAsync(IAppDbContext db, ISiteAccess access, Guid id, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var task = await db.SiteTasks.Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == id, ct);
        return task is not null && scope.Includes(task.SiteId) ? task : null;
    }

    public static Error? CheckKind(SiteTask task, params TaskKind[] allowed) =>
        allowed.Contains(task.Kind)
            ? null
            : Error.Conflict("task.wrong_kind", $"This action does not apply to {task.Kind} tasks.");

    /// <summary>The task must be open (or missed but still inside the late window) and already available.</summary>
    public static Error? CheckWorkable(SiteTask task, DateTime now)
    {
        if (!task.CanBeWorkedOn(now))
        {
            return task.Status == SiteTaskStatus.Missed
                ? Error.Conflict("task.late_window_closed", "This missed task can no longer be completed.")
                : Error.Conflict("task.not_open", "The task is not open.");
        }

        return task.AvailableAt is { } available && now < available
            ? Error.Conflict("task.not_available_yet", "The task is not available yet.")
            : null;
    }

    public static Error InvalidAnswers(IReadOnlyList<AnswerProblem> problems) => Error.Validation(
        "task.invalid_answers", "Some answers are not valid.",
        problems.GroupBy(p => $"answers.{p.FieldKey}").ToDictionary(g => g.Key, g => g.Select(p => p.Message).ToArray()));

    public static Error PhotoRequired() => Error.Validation(
        "task.photo_required", "This task requires at least one photo.",
        new Dictionary<string, string[]> { ["photoIds"] = ["Attach at least one photo."] });

    /// <summary>
    /// Every photo and signature referenced by an entry must be an uploaded file of this organization taken at
    /// the task's own site, and of the right kind.
    /// </summary>
    public static async Task<Error?> CheckAttachmentsAsync(
        IAppDbContext db, SiteTask task, IReadOnlyDictionary<string, FieldAnswer> answers,
        IEnumerable<Guid> taskPhotoIds, CancellationToken ct)
    {
        var photos = answers.Values.SelectMany(a => a.PhotoIds).Concat(taskPhotoIds).ToHashSet();
        var signatures = new HashSet<Guid>();
        foreach (var field in task.Fields.Where(f => f.Type == FieldType.Signature))
        {
            if (answers.TryGetValue(field.Key, out var answer)
                && answer.Value is { ValueKind: JsonValueKind.String } text
                && Guid.TryParse(text.GetString(), out var id))
                signatures.Add(id);
        }

        var wanted = photos.Concat(signatures).ToList();
        if (wanted.Count == 0) return null;

        var siteId = task.SiteId;
        var found = await db.Attachments.AsNoTracking()
            .Where(a => wanted.Contains(a.Id) && a.SiteId == siteId)
            .Select(a => new { a.Id, a.Kind })
            .ToDictionaryAsync(a => a.Id, a => a.Kind, ct);

        var valid = photos.All(id => found.TryGetValue(id, out var kind) && kind == AttachmentKind.Photo)
                    && signatures.All(id => found.TryGetValue(id, out var kind) && kind == AttachmentKind.Signature);
        return valid
            ? null
            : Error.Validation("task.unknown_attachments", "Some photos or signatures do not exist for this site, or are the wrong kind.");
    }

    /// <summary>Answers, note and task-level photos as sent by a client; shared by progress, entries and completion.</summary>
    public static void AddCommonRules<T>(
        AbstractValidator<T> validator,
        Func<T, IReadOnlyDictionary<string, FieldAnswer>?> answers,
        Func<T, string?> note,
        Func<T, IReadOnlyList<Guid>?> photoIds)
    {
        validator.RuleFor(x => answers(x))
            .Must(a => a is null || a.Count <= MaxAnswers)
            .WithMessage($"Send at most {MaxAnswers} answers.")
            .Must(a => a is null || a.Values.All(v => v is not null))
            .WithMessage("An answer cannot be null.")
            .OverridePropertyName("answers");
        validator.RuleFor(x => note(x)).MaximumLength(SiteTask.NoteMaxLength).OverridePropertyName("note");
        validator.RuleFor(x => photoIds(x))
            .Must(p => p is null || p.Count <= MaxTaskPhotos)
            .WithMessage($"Attach at most {MaxTaskPhotos} photos.")
            .OverridePropertyName("photoIds");
    }
}
