using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Trail.Application.Common.Abstractions;
using Trail.Application.Features.Issues;
using Trail.Domain.Common;
using Trail.Domain.Issues;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.Infrastructure.Persistence;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Issues;

public sealed class IssueRaisingTests(MySqlFixture mysql) : IAsyncLifetime
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);

    private TrailApiFactory _factory = null!;
    private Guid _org;
    private SiteTask _task = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _org = (await _factory.CreateOrganizationAsync()).OrganizationId;
        await _factory.WithDbAsync(_org, async db =>
        {
            var site = new Site { Name = "Leeds", TimeZone = "Europe/London" };
            var template = new TaskTemplate
            {
                Name = "Fridge log",
                Kind = TaskKind.RecordLog,
                Fields =
                [
                    new TemplateField { Key = "temp", Label = "Fridge temperature", Type = FieldType.Temperature, Max = 5 },
                    new TemplateField { Key = "seal", Label = "Door seal intact", Type = FieldType.YesNo, ExpectedAnswer = true },
                    new TemplateField
                    {
                        Key = "label", Label = "Food labelled", Type = FieldType.YesNo, ExpectedAnswer = true, RaiseIssueOnFailure = false,
                    },
                ],
            };
            db.Sites.Add(site);
            db.TaskTemplates.Add(template);
            _task = SiteTask.FromTemplate(template, site.Id, SiteTime.Zone(site.TimeZone), new DateOnly(2026, 3, 10),
                new TimeOnly(10, 0), null, SiteTaskSource.AdHoc);
            db.SiteTasks.Add(_task);
            await db.SaveChangesAsync();
            return 0;
        });
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private static TaskEntry Reading(decimal temperature, bool seal, bool labelled, string? comment = null, Guid? photo = null)
    {
        var entry = new TaskEntry
        {
            Answers = new()
            {
                ["temp"] = new FieldAnswer
                {
                    Value = JsonSerializer.SerializeToElement(temperature), Comment = comment, PhotoIds = photo is { } p ? [p] : [],
                },
                ["seal"] = new FieldAnswer { Value = JsonSerializer.SerializeToElement(seal) },
                ["label"] = new FieldAnswer { Value = JsonSerializer.SerializeToElement(labelled) },
            },
        };
        return entry;
    }

    /// <summary>Evaluates, saves the entry and raises issues for it in one unit of work, like the completion flow.</summary>
    private async Task<IReadOnlyList<Issue>> SubmitAsync(TaskEntry entry)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<ITenantContext>().UseOrganization(_org);
        var db = scope.ServiceProvider.GetRequiredService<TrailDbContext>();
        var task = await db.SiteTasks.SingleAsync(t => t.Id == _task.Id);
        entry.SiteTaskId = task.Id;
        entry.Failures = [.. AnswerEvaluator.Evaluate(task.Fields, entry.Answers, requireAll: false).Failures];
        db.TaskEntries.Add(entry);
        var raised = await scope.ServiceProvider.GetRequiredService<IssueRaiser>()
            .RaiseForFailuresAsync(task, entry, Now, null, CancellationToken.None);
        await db.SaveChangesAsync();
        return raised;
    }

    private Task<List<Issue>> IssuesAsync() =>
        _factory.WithDbAsync(_org, db => db.Issues.AsNoTracking().Include(i => i.Events).OrderBy(i => i.Number).ToListAsync());

    [Fact]
    public async Task Each_failing_field_raises_one_issue_unless_the_field_opts_out()
    {
        var photo = Guid.CreateVersion7();
        var raised = await SubmitAsync(Reading(9.5m, seal: false, labelled: false, comment: "Moved stock", photo: photo));

        raised.Count.ShouldBe(2);
        var issues = await IssuesAsync();
        issues.Select(i => i.Number).ShouldBe([1, 2]);

        var temperature = issues.Single(i => i.FieldKey == "temp");
        temperature.Title.ShouldBe("Fridge temperature: 9.5°C is above the maximum of 5°C.");
        temperature.Priority.ShouldBe(IssuePriority.High);
        temperature.DueAt.ShouldBe(Now.AddHours(24));
        temperature.Source.ShouldBe(IssueSource.TaskFailure);
        temperature.SiteTaskId.ShouldBe(_task.Id);
        temperature.SiteId.ShouldBe(_task.SiteId);
        temperature.PhotoIds.ShouldBe([photo]);
        temperature.Description!.ShouldContain("Action noted: Moved stock");
        temperature.Events.ShouldHaveSingleItem().Kind.ShouldBe(IssueEventKind.Raised);

        issues.Single(i => i.FieldKey == "seal").Priority.ShouldBe(IssuePriority.Medium);
        issues.ShouldNotContain(i => i.FieldKey == "label");
    }

    [Fact]
    public async Task A_repeat_failure_comments_on_the_active_issue_instead_of_raising_another()
    {
        await SubmitAsync(Reading(8m, seal: true, labelled: true));
        (await SubmitAsync(Reading(4m, seal: true, labelled: true))).ShouldBeEmpty();
        (await SubmitAsync(Reading(11m, seal: true, labelled: true))).ShouldBeEmpty();

        var issue = (await IssuesAsync()).ShouldHaveSingleItem();
        issue.Events.Select(e => e.Kind).ShouldBe([IssueEventKind.Raised, IssueEventKind.Comment]);
        issue.Events[1].Note!.ShouldStartWith("Failed again: 11°C is above");

        // Once resolved, the next failure is a new problem.
        await _factory.WithDbAsync(_org, async db =>
        {
            (await db.Issues.SingleAsync()).Resolve(Now, null, "Engineer replaced the thermostat.", []);
            return await db.SaveChangesAsync();
        });
        (await SubmitAsync(Reading(12m, seal: true, labelled: true))).ShouldHaveSingleItem().Number.ShouldBe(2);
    }

    [Fact]
    public async Task Issue_numbers_are_unique_under_concurrency_and_per_organization()
    {
        var other = (await _factory.CreateOrganizationAsync("Other")).OrganizationId;

        async Task<long> Allocate(Guid organizationId, int count)
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<ITenantContext>().UseOrganization(organizationId);
            return await scope.ServiceProvider.GetRequiredService<ISequenceAllocator>()
                .NextAsync(OrganizationSequence.Issues, count, CancellationToken.None);
        }

        var firsts = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Allocate(_org, 3)));

        var numbers = firsts.SelectMany(f => new[] { f, f + 1, f + 2 }).ToList();
        numbers.Distinct().Count().ShouldBe(60);
        numbers.Min().ShouldBe(1);
        numbers.Max().ShouldBe(60);
        (await Allocate(other, 1)).ShouldBe(1);
        (await Allocate(_org, 1)).ShouldBe(61);
    }
}
