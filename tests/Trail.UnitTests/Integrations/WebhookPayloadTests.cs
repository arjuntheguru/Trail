using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Trail.Application;
using Trail.Application.Common.Events;
using Trail.Application.Features.Integrations;
using Trail.Domain.Integrations;
using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Integrations;

public sealed class WebhookPayloadTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 30, 0, DateTimeKind.Utc);
    private static readonly Guid Org = Guid.CreateVersion7();
    private static readonly Guid EventId = Guid.CreateVersion7();

    private static Issue NewIssue()
    {
        var issue = new Issue { Title = "Fridge 2 above 5C", SiteId = Guid.CreateVersion7(), Number = 42, Priority = IssuePriority.High };
        issue.Raised(Now, null, Now.AddHours(4));
        return issue;
    }

    [Fact]
    public void Envelope_has_id_type_time_organization_and_data()
    {
        var issue = NewIssue();

        var json = JsonDocument.Parse(WebhookPayloads.Build(EventId, WebhookEventTypes.IssueRaised, Now, Org, WebhookPayloads.ForIssue(issue))).RootElement;

        json.EnumerateObject().Select(p => p.Name).ShouldBe(["id", "type", "occurredAt", "organizationId", "data"]);
        json.GetProperty("id").GetGuid().ShouldBe(EventId);
        json.GetProperty("type").GetString().ShouldBe("issue.raised");
        json.GetProperty("occurredAt").GetString().ShouldBe("2026-03-10T09:30:00Z");
        json.GetProperty("organizationId").GetGuid().ShouldBe(Org);
    }

    [Fact]
    public void Issue_data_is_a_compact_camel_case_snapshot_with_string_enums()
    {
        var issue = NewIssue();

        var data = Data(WebhookEventTypes.IssueRaised, WebhookPayloads.ForIssue(issue));

        data.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["id", "number", "title", "siteId", "priority", "status", "assigneeMembershipId", "dueAt"]);
        data.GetProperty("id").GetGuid().ShouldBe(issue.Id);
        data.GetProperty("number").GetInt32().ShouldBe(42);
        data.GetProperty("title").GetString().ShouldBe("Fridge 2 above 5C");
        data.GetProperty("siteId").GetGuid().ShouldBe(issue.SiteId);
        data.GetProperty("priority").GetString().ShouldBe("High");
        data.GetProperty("status").GetString().ShouldBe("Open");
        data.GetProperty("assigneeMembershipId").ValueKind.ShouldBe(JsonValueKind.Null);
        data.GetProperty("dueAt").GetString().ShouldBe("2026-03-10T13:30:00Z");
    }

    [Fact]
    public void Issue_changes_add_a_change_object()
    {
        var issue = NewIssue();
        var membership = Guid.CreateVersion7();

        var assigned = Data(WebhookEventTypes.IssueAssigned, WebhookPayloads.ForIssue(issue, new AssigneeChange(null, membership)));
        assigned.GetProperty("change").GetProperty("to").GetGuid().ShouldBe(membership);
        assigned.GetProperty("change").GetProperty("from").ValueKind.ShouldBe(JsonValueKind.Null);

        var status = Data(WebhookEventTypes.IssueStatusChanged, WebhookPayloads.ForIssue(issue, new StatusChange(IssueStatus.Open, IssueStatus.InProgress)));
        status.GetProperty("change").GetProperty("from").GetString().ShouldBe("Open");
        status.GetProperty("change").GetProperty("to").GetString().ShouldBe("InProgress");
    }

    [Fact]
    public void Task_data_has_the_documented_fields()
    {
        var template = new TaskTemplate { Name = "Opening checks", Kind = TaskKind.Checklist };
        var task = SiteTask.FromTemplate(
            template, Guid.CreateVersion7(), TimeZoneInfo.Utc, new DateOnly(2026, 3, 10), new TimeOnly(17, 0), null, SiteTaskSource.AdHoc);
        task.Complete(Now, Guid.CreateVersion7(), hasFailures: true);

        var data = Data(WebhookEventTypes.TaskCompleted, WebhookPayloads.ForTask(task));

        data.EnumerateObject().Select(p => p.Name).ShouldBe(
            ["id", "siteId", "title", "dueDate", "status", "completedAt", "completedLate", "hasFailures"]);
        data.GetProperty("title").GetString().ShouldBe("Opening checks");
        data.GetProperty("dueDate").GetString().ShouldBe("2026-03-10");
        data.GetProperty("status").GetString().ShouldBe("Completed");
        data.GetProperty("completedAt").GetString().ShouldBe("2026-03-10T09:30:00Z");
        data.GetProperty("completedLate").GetBoolean().ShouldBeFalse();
        data.GetProperty("hasFailures").GetBoolean().ShouldBeTrue();
    }

    [Fact]
    public void Payloads_never_contain_secrets_or_internal_fields()
    {
        var body = WebhookPayloads.Build(EventId, WebhookEventTypes.IssueRaised, Now, Org, WebhookPayloads.ForIssue(NewIssue()));

        body.ShouldNotContain("secret", Case.Insensitive);
        body.ShouldNotContain("concurrency", Case.Insensitive);
        body.ShouldNotContain("description", Case.Insensitive);
    }

    [Fact]
    public void Event_catalog_is_stable_and_excludes_ping()
    {
        WebhookEventTypes.All.ShouldBe(
            ["issue.raised", "issue.assigned", "issue.status_changed", "issue.commented", "task.completed", "task.approved"]);
        WebhookEventTypes.IsKnown("ping").ShouldBeFalse();
        WebhookEventTypes.IsKnown("issue.raised").ShouldBeTrue();
        WebhookEventTypes.IsKnown("Issue.Raised").ShouldBeFalse();
    }

    [Fact]
    public void Every_catalog_event_has_a_registered_domain_event_handler()
    {
        var services = new ServiceCollection().AddApplication();

        foreach (var eventType in new[]
                 {
                     typeof(IssueRaised), typeof(IssueAssigned), typeof(IssueStatusChanged), typeof(IssueCommented),
                     typeof(TaskCompleted), typeof(TaskApproved),
                 })
        {
            var contract = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
            services.Count(d => d.ServiceType == contract && d.ImplementationType!.Name.EndsWith("WebhookHandler", StringComparison.Ordinal))
                .ShouldBe(1, $"expected exactly one webhook handler for {eventType.Name}");
        }
    }

    private static JsonElement Data(string type, object data) =>
        JsonDocument.Parse(WebhookPayloads.Build(EventId, type, Now, Org, data)).RootElement.GetProperty("data").Clone();
}
