using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Domain.Issues;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Issues;

/// <summary>
/// Turns failing answers into issues so every failure gets a tracked corrective action. One active issue per
/// task field: a further failure of the same field (another bad reading on a log) is added to it as a comment.
/// Adds to the unit of work; the caller saves.
/// </summary>
public sealed class IssueRaiser(IAppDbContext db, ISequenceAllocator sequences)
{
    public async Task<IReadOnlyList<Issue>> RaiseForFailuresAsync(
        SiteTask task, TaskEntry entry, DateTime now, Guid? userId, CancellationToken ct)
    {
        var fields = task.Fields.ToDictionary(f => f.Key, StringComparer.Ordinal);
        var failures = entry.Failures
            .Where(f => fields.TryGetValue(f.FieldKey, out var field) && field.RaiseIssueOnFailure)
            .ToList();
        if (failures.Count == 0) return [];

        var keys = failures.Select(f => f.FieldKey).ToList();
        var active = await db.Issues
            .Where(i => i.SiteTaskId == task.Id && i.FieldKey != null && keys.Contains(i.FieldKey)
                        && (i.Status == IssueStatus.Open || i.Status == IssueStatus.InProgress))
            .ToListAsync(ct);

        var raised = new List<Issue>();
        foreach (var failure in failures)
        {
            entry.Answers.TryGetValue(failure.FieldKey, out var answer);
            var photos = answer?.PhotoIds ?? [];
            var existing = active.FirstOrDefault(i => i.FieldKey == failure.FieldKey);
            if (existing is not null)
            {
                existing.Comment(now, userId, Describe(failure, answer), photos);
                continue;
            }

            var priority = fields[failure.FieldKey].Type == FieldType.Temperature ? IssuePriority.High : IssuePriority.Medium;
            var issue = new Issue
            {
                Title = IssuePolicy.Truncate($"{failure.Label}: {failure.Reason}", Issue.TitleMaxLength),
                Description = IssuePolicy.Truncate(
                    $"Raised automatically from \"{task.Title}\" due {task.DueDate:yyyy-MM-dd}."
                    + (string.IsNullOrWhiteSpace(answer?.Comment) ? "" : $"\n\nAction noted: {answer.Comment.Trim()}"),
                    Issue.DescriptionMaxLength),
                SiteId = task.SiteId,
                DepartmentId = task.DepartmentId,
                Priority = priority,
                Source = IssueSource.TaskFailure,
                SiteTaskId = task.Id,
                TaskEntryId = entry.Id,
                FieldKey = failure.FieldKey,
                PhotoIds = [.. photos],
            };
            issue.Raised(now, userId, now + IssuePolicy.DefaultResponseTime(priority));
            raised.Add(issue);
        }

        if (raised.Count > 0)
        {
            var first = await sequences.NextAsync(OrganizationSequence.Issues, raised.Count, ct);
            for (var i = 0; i < raised.Count; i++) raised[i].Number = checked((int)(first + i));
            db.Issues.AddRange(raised);
        }
        return raised;
    }

    private static string Describe(FieldFailure failure, FieldAnswer? answer) =>
        IssuePolicy.Truncate(
            $"Failed again: {failure.Reason}" + (string.IsNullOrWhiteSpace(answer?.Comment) ? "" : $" Action noted: {answer.Comment.Trim()}"),
            Issue.NoteMaxLength);
}
