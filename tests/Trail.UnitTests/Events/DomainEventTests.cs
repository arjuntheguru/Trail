using Microsoft.Extensions.DependencyInjection;
using Trail.Application;
using Trail.Application.Common.Events;
using Trail.Domain.Common;
using Trail.Domain.Issues;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Events;

public sealed class DomainEventTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Alice = Guid.CreateVersion7();
    private static readonly Guid Bob = Guid.CreateVersion7();

    [Fact]
    public void Issue_changes_raise_one_event_each_in_order()
    {
        var issue = new Issue { Title = "Fridge warm", SiteId = Guid.CreateVersion7() };

        issue.Raised(Now, Alice, null);
        issue.Assign(Now, Alice, Bob);
        issue.Assign(Now, Alice, Bob); // unchanged: nothing raised
        issue.Comment(Now, Bob, "On it", []);
        issue.Resolve(Now, Bob, "  Replaced seal. ", []);

        issue.DomainEvents.Select(e => e.GetType()).ShouldBe(
            [typeof(IssueRaised), typeof(IssueAssigned), typeof(IssueCommented), typeof(IssueStatusChanged)]);

        var assigned = (IssueAssigned)issue.DomainEvents[1];
        assigned.PreviousAssigneeId.ShouldBeNull();
        assigned.AssigneeId.ShouldBe(Bob);
        ((IssueCommented)issue.DomainEvents[2]).EventId.ShouldBe(issue.Events[2].Id);
        var resolved = (IssueStatusChanged)issue.DomainEvents[3];
        (resolved.From, resolved.To, resolved.Note, resolved.ActorId).ShouldBe((IssueStatus.Open, IssueStatus.Resolved, "Replaced seal.", Bob));

        issue.ClearDomainEvents();
        issue.DomainEvents.ShouldBeEmpty();
    }

    [Fact]
    public void Completing_and_approving_a_task_raise_events()
    {
        var template = new TaskTemplate { Name = "Close down", RequiresApproval = true };
        var task = SiteTask.FromTemplate(template, Guid.CreateVersion7(), TimeZoneInfo.Utc, new DateOnly(2026, 3, 10),
            new TimeOnly(22, 0), null, SiteTaskSource.Scheduled);

        task.Complete(Now, Alice, hasFailures: false);
        task.Approve(Now.AddHours(1), Bob, null);

        task.DomainEvents.ShouldBe(
        [
            new TaskCompleted(task.Id, task.SiteId, Alice, Now),
            new TaskApproved(task.Id, task.SiteId, Bob, Now.AddHours(1)),
        ]);
    }

    [Fact]
    public async Task The_dispatcher_runs_every_handler_registered_for_the_event_type()
    {
        var services = new ServiceCollection().AddApplication();
        var log = new List<string>();
        services.AddSingleton(log);
        services.AddScoped<IDomainEventHandler<TaskCompleted>, First>();
        services.AddScoped<IDomainEventHandler<TaskCompleted>, Second>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDomainEventDispatcher>();
        await dispatcher.DispatchAsync(
        [
            new TaskCompleted(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Now),
            new TaskApproved(Guid.CreateVersion7(), Guid.CreateVersion7(), null, Now), // no handlers: ignored
        ], CancellationToken.None);

        log.ShouldBe(["first", "second"]);
    }

    private sealed class First(List<string> log) : IDomainEventHandler<TaskCompleted>
    {
        public Task Handle(TaskCompleted domainEvent, CancellationToken ct)
        {
            log.Add("first");
            return Task.CompletedTask;
        }
    }

    private sealed class Second(List<string> log) : IDomainEventHandler<TaskCompleted>
    {
        public Task Handle(TaskCompleted domainEvent, CancellationToken ct)
        {
            log.Add("second");
            return Task.CompletedTask;
        }
    }
}
