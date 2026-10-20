using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Persistence;

public sealed class TaskPersistenceTests(MySqlFixture mysql) : IAsyncLifetime
{
    private TestDb _db = null!;
    private Guid _org;
    private Guid _site;
    private TaskTemplate _template = null!;

    public async ValueTask InitializeAsync()
    {
        _db = await TestDb.CreateAsync(mysql);
        await using (var context = _db.Context(TestUser.Anonymous))
        {
            var organization = new Organization { Name = "Alpha Foods", Slug = $"alpha-{Guid.NewGuid():N}" };
            context.Organizations.Add(organization);
            await context.SaveChangesAsync(Ct);
            _org = organization.Id;
        }

        await using var write = _db.ContextFor(_org);
        var site = new Site { Name = "Leeds", TimeZone = "Europe/London" };
        _template = new TaskTemplate
        {
            Name = "Fridge check",
            Kind = TaskKind.Checklist,
            Fields =
            [
                new TemplateField { Key = "temp", Label = "Fridge temperature", Type = FieldType.Temperature, Min = 0, Max = 5m },
                new TemplateField
                {
                    Key = "state", Label = "Condition", Type = FieldType.SingleSelect,
                    Options = [new("ok", "OK"), new("dirty", "Dirty", IsFailure: true)],
                },
            ],
        };
        write.Sites.Add(site);
        write.TaskTemplates.Add(_template);
        await write.SaveChangesAsync(Ct);
        _site = site.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SiteTask NewTask(Guid? scheduleId, DateOnly date) => new()
    {
        SiteId = _site,
        TemplateId = _template.Id,
        TemplateVersion = _template.Version,
        ScheduleId = scheduleId,
        Title = _template.Name,
        Kind = _template.Kind,
        Fields = _template.Fields,
        ScheduledDate = date,
        DueDate = date,
        DueAt = date.ToDateTime(new TimeOnly(10, 0), DateTimeKind.Utc),
    };

    [Fact]
    public async Task Schedules_keep_dates_times_and_weekday_flags()
    {
        Guid id;
        await using (var write = _db.ContextFor(_org))
        {
            var schedule = new TaskSchedule
            {
                TemplateId = _template.Id, Frequency = Frequency.Weekly, Interval = 2,
                StartDate = new DateOnly(2026, 3, 1), EndDate = new DateOnly(2026, 12, 31),
                Days = WeekDays.Monday | WeekDays.Thursday, DueTime = new TimeOnly(9, 45), AvailableFrom = new TimeOnly(6, 0),
                Sites = [new TaskScheduleSite { SiteId = _site }],
            };
            write.TaskSchedules.Add(schedule);
            await write.SaveChangesAsync(Ct);
            id = schedule.Id;
        }

        await using var read = _db.ContextFor(_org);
        var loaded = await read.TaskSchedules.AsNoTracking().Include(s => s.Sites).SingleAsync(s => s.Id == id, Ct);

        loaded.Days.ShouldBe(WeekDays.Monday | WeekDays.Thursday);
        loaded.StartDate.ShouldBe(new DateOnly(2026, 3, 1));
        loaded.EndDate.ShouldBe(new DateOnly(2026, 12, 31));
        loaded.DueTime.ShouldBe(new TimeOnly(9, 45));
        loaded.AvailableFrom.ShouldBe(new TimeOnly(6, 0));
        loaded.Sites.ShouldHaveSingleItem().SiteId.ShouldBe(_site);
    }

    [Fact]
    public async Task Task_fields_and_entry_answers_survive_the_json_columns()
    {
        var task = NewTask(null, new DateOnly(2026, 1, 15));
        var photo = Guid.CreateVersion7();
        task.Entries.Add(new TaskEntry
        {
            Answers = new()
            {
                ["temp"] = new FieldAnswer { Value = JsonSerializer.SerializeToElement(7.5m), Comment = "Moved stock", PhotoIds = [photo] },
                ["state"] = new FieldAnswer { Value = JsonSerializer.SerializeToElement("ok") },
            },
            Failures = [new FieldFailure("temp", "Fridge temperature", "7.5°C is above the maximum of 5°C.")],
            PhotoIds = [photo],
        });
        await using (var write = _db.ContextFor(_org))
        {
            write.SiteTasks.Add(task);
            await write.SaveChangesAsync(Ct);
        }

        await using var read = _db.ContextFor(_org);
        var loaded = await read.SiteTasks.AsNoTracking().Include(t => t.Entries).SingleAsync(t => t.Id == task.Id, Ct);

        loaded.Fields.Count.ShouldBe(2);
        loaded.Fields[0].Max.ShouldBe(5m);
        loaded.Fields[1].Options[1].IsFailure.ShouldBeTrue();
        var entry = loaded.Entries.ShouldHaveSingleItem();
        entry.Answers["temp"].Value!.Value.GetDecimal().ShouldBe(7.5m);
        entry.Answers["temp"].PhotoIds.ShouldBe([photo]);
        entry.Answers["state"].Value!.Value.GetString().ShouldBe("ok");
        entry.Failures.ShouldHaveSingleItem().FieldKey.ShouldBe("temp");
        entry.HasFailures.ShouldBeTrue();

        // The evaluator works on what comes back from the database.
        AnswerEvaluator.Evaluate(loaded.Fields, entry.Answers, requireAll: true).Failures.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task A_schedule_produces_at_most_one_task_per_site_and_day()
    {
        var schedule = new TaskSchedule { TemplateId = _template.Id, Frequency = Frequency.Daily, AllSites = true, StartDate = new DateOnly(2026, 1, 1) };
        await using (var write = _db.ContextFor(_org))
        {
            write.TaskSchedules.Add(schedule);
            write.SiteTasks.Add(NewTask(schedule.Id, new DateOnly(2026, 1, 15)));
            write.SiteTasks.Add(NewTask(null, new DateOnly(2026, 1, 15)));
            write.SiteTasks.Add(NewTask(null, new DateOnly(2026, 1, 15)));
            await write.SaveChangesAsync(Ct);
        }

        await using var duplicate = _db.ContextFor(_org);
        duplicate.SiteTasks.Add(NewTask(schedule.Id, new DateOnly(2026, 1, 15)));
        await Should.ThrowAsync<DbUpdateException>(() => duplicate.SaveChangesAsync(Ct));
    }
}
