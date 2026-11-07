using System.Text.Json;
using System.Text.Json.Serialization;
using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Integrations;

/// <summary>The envelope every webhook body has. <see cref="Data"/> depends on <see cref="Type"/>.</summary>
public sealed record WebhookEnvelope(Guid Id, string Type, DateTime OccurredAt, Guid OrganizationId, object Data);

/// <summary>Compact snapshot of an issue at the time of the event. <see cref="Change"/> says what happened, where useful.</summary>
public sealed record IssueWebhookData(
    Guid Id, int Number, string Title, Guid SiteId, IssuePriority Priority, IssueStatus Status,
    Guid? AssigneeMembershipId, DateTime? DueAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] object? Change = null);

public sealed record TaskWebhookData(
    Guid Id, Guid SiteId, string Title, DateOnly DueDate, SiteTaskStatus Status,
    DateTime? CompletedAt, bool CompletedLate, bool HasFailures);

public sealed record StatusChange(IssueStatus From, IssueStatus To);

public sealed record AssigneeChange(Guid? From, Guid? To);

public sealed record CommentChange(Guid EventId);

public sealed record PingWebhookData(string Message);

/// <summary>
/// Builds the JSON bodies sent to subscribers. Pure so the exact shape (a contract customers code against) is
/// unit-tested. Camel-cased property names and string enums, like the rest of the API. Never put secrets,
/// tokens or personal data beyond ids in here.
/// </summary>
public static class WebhookPayloads
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Build(Guid eventId, string eventType, DateTime occurredAt, Guid organizationId, object data) =>
        JsonSerializer.Serialize(
            new WebhookEnvelope(eventId, eventType, DateTime.SpecifyKind(occurredAt, DateTimeKind.Utc), organizationId, data),
            Options);

    public static IssueWebhookData ForIssue(Issue issue, object? change = null) => new(
        issue.Id, issue.Number, issue.Title, issue.SiteId, issue.Priority, issue.Status,
        issue.AssigneeId, issue.DueAt is { } due ? DateTime.SpecifyKind(due, DateTimeKind.Utc) : null, change);

    public static TaskWebhookData ForTask(SiteTask task) => new(
        task.Id, task.SiteId, task.Title, task.DueDate, task.Status,
        task.CompletedAt is { } done ? DateTime.SpecifyKind(done, DateTimeKind.Utc) : null,
        task.CompletedLate, task.HasFailures);
}
