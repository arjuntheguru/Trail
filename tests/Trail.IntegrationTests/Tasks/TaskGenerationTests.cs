using Microsoft.EntityFrameworkCore;
using Trail.Domain.Organizations;
using Trail.Domain.Tasks;
using Trail.IntegrationTests.Infrastructure;

namespace Trail.IntegrationTests.Tasks;

public sealed class TaskGenerationTests(MySqlFixture mysql) : IAsyncLifetime
{
    // A Tuesday. London is on GMT (UTC+0) and Sydney on AEDT (UTC+11), so at 23:30 UTC Sydney is already on the 11th.
    private static readonly DateTimeOffset Evening = new(2026, 3, 10, 23, 30, 0, TimeSpan.Zero);
    private static readonly DateOnly March10 = new(2026, 3, 10);

    private TrailApiFactory _factory = null!;
    private Guid _org;
    private Guid _north;
    private Site _london = null!;
    private Site _sydney = null!;
    private Site _closed = null!;
    private TaskTemplate _template = null!;

    public async ValueTask InitializeAsync()
    {
        _factory = await TrailApiFactory.CreateAsync(mysql);
        _org = (await _factory.CreateOrganizationAsync()).OrganizationId;
        await Db(async db =>
        {
            var north = new Region { Name = "North" };
            db.Regions.Add(north);
            _london = new Site { Name = "London", TimeZone = "Europe/London", RegionId = north.Id };
            _sydney = new Site { Name = "Sydney", TimeZone = "Australia/Sydney" };
            _closed = new Site { Name = "Closed", TimeZone = "Europe/London", IsActive = false };
            _template = new TaskTemplate
            {
                Name = "Opening checks",
                Kind = TaskKind.Checklist,
                RequiresApproval = true,
                Fields = [new TemplateField { Key = "doors", Label = "Doors unlocked", Type = FieldType.Checkbox }],
            };
            db.Sites.AddRange(_london, _sydney, _closed);
            db.TaskTemplates.Add(_template);
            await db.SaveChangesAsync();
            _north = north.Id;
        });
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    private Task Db(Func<Trail.Infrastructure.Persistence.TrailDbContext, Task> action) =>
        _factory.WithDbAsync(_org, async db => { await action(db); return 0; });

    private async Task<TaskSchedule> AddScheduleAsync(Action<TaskSchedule> configure)
    {
        var schedule = new TaskSchedule
        {
            TemplateId = _template.Id, Frequency = Frequency.Daily, StartDate = new DateOnly(2026, 1, 1), DueTime = new TimeOnly(10, 0),
        };
        configure(schedule);
        await Db(async db => { db.TaskSchedules.Add(schedule); await db.SaveChangesAsync(); });
        return schedule;
    }

    private Task<List<SiteTask>> TasksAsync() =>
        _factory.WithDbAsync(_org, db => db.SiteTasks.AsNoTracking().OrderBy(t => t.ScheduledDate).ToListAsync());

    [Fact]
    public async Task Each_active_site_gets_today_and_tomorrow_in_its_own_time_zone()
    {
        var schedule = await AddScheduleAsync(s => { s.AllSites = true; s.AvailableFrom = new TimeOnly(6, 0); });

        (await _factory.RunTaskGenerationAsync(_org, Evening)).Created.ShouldBe(4);

        var tasks = await TasksAsync();
        tasks.Where(t => t.SiteId == _london.Id).Select(t => t.ScheduledDate).ShouldBe([March10, March10.AddDays(1)]);
        tasks.Where(t => t.SiteId == _sydney.Id).Select(t => t.ScheduledDate).ShouldBe([March10.AddDays(1), March10.AddDays(2)]);
        tasks.ShouldNotContain(t => t.SiteId == _closed.Id);

        var london = tasks.First(t => t.SiteId == _london.Id);
        london.DueAt.ShouldBe(new DateTime(2026, 3, 10, 10, 0, 0, DateTimeKind.Utc));
        london.AvailableAt.ShouldBe(new DateTime(2026, 3, 10, 6, 0, 0, DateTimeKind.Utc));
        var sydney = tasks.First(t => t.SiteId == _sydney.Id);
        sydney.DueAt.ShouldBe(new DateTime(2026, 3, 10, 23, 0, 0, DateTimeKind.Utc)); // 10:00 AEDT on the 11th

        // The snapshot comes from the template.
        london.Title.ShouldBe("Opening checks");
        london.RequiresApproval.ShouldBeTrue();
        london.Fields.ShouldHaveSingleItem().Key.ShouldBe("doors");
        london.ScheduleId.ShouldBe(schedule.Id);
        london.Source.ShouldBe(SiteTaskSource.Scheduled);
        london.Status.ShouldBe(SiteTaskStatus.Open);
    }

    [Fact]
    public async Task Running_again_creates_nothing_new()
    {
        await AddScheduleAsync(s => s.AllSites = true);

        (await _factory.RunTaskGenerationAsync(_org, Evening)).Created.ShouldBe(4);
        (await _factory.RunTaskGenerationAsync(_org, Evening)).Created.ShouldBe(0);
        (await _factory.RunTaskGenerationAsync(_org, Evening.AddHours(1))).Created.ShouldBe(1); // London moves to the 11th

        (await TasksAsync()).Count.ShouldBe(5);
    }

    [Fact]
    public async Task Overlapping_runs_do_not_duplicate_tasks()
    {
        await AddScheduleAsync(s => s.AllSites = true);

        var runs = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => _factory.RunTaskGenerationAsync(_org, Evening)));

        runs.Sum(r => r.Created).ShouldBe(4);
        (await TasksAsync()).Count.ShouldBe(4);
    }

    [Fact]
    public async Task Schedules_target_listed_sites_and_regions_only()
    {
        await AddScheduleAsync(s => s.Regions = [new TaskScheduleRegion { RegionId = _north }]);
        await AddScheduleAsync(s => s.Sites = [new TaskScheduleSite { SiteId = _sydney.Id }]);

        await _factory.RunTaskGenerationAsync(_org, Evening);

        var tasks = await TasksAsync();
        tasks.Count(t => t.SiteId == _london.Id).ShouldBe(2);
        tasks.Count(t => t.SiteId == _sydney.Id).ShouldBe(2);
        tasks.Select(t => t.ScheduleId).Distinct().Count().ShouldBe(2);
    }

    [Fact]
    public async Task Recurrence_days_are_respected()
    {
        // Tuesday the 10th is excluded, Wednesday the 11th is included.
        await AddScheduleAsync(s => { s.AllSites = true; s.Frequency = Frequency.Weekly; s.Days = WeekDays.Wednesday; });

        await _factory.RunTaskGenerationAsync(_org, Evening);

        (await TasksAsync()).Where(t => t.SiteId == _london.Id).ShouldHaveSingleItem().ScheduledDate.ShouldBe(March10.AddDays(1));
    }

    [Fact]
    public async Task Inactive_or_deleted_schedules_and_inactive_templates_generate_nothing()
    {
        await AddScheduleAsync(s => { s.AllSites = true; s.IsActive = false; });
        var deleted = await AddScheduleAsync(s => s.AllSites = true);
        await Db(async db =>
        {
            db.TaskSchedules.Remove(await db.TaskSchedules.SingleAsync(s => s.Id == deleted.Id));
            await db.SaveChangesAsync();
        });

        (await _factory.RunTaskGenerationAsync(_org, Evening)).Created.ShouldBe(0);

        await AddScheduleAsync(s => s.AllSites = true);
        await Db(async db =>
        {
            (await db.TaskTemplates.SingleAsync(t => t.Id == _template.Id)).IsActive = false;
            await db.SaveChangesAsync();
        });
        (await _factory.RunTaskGenerationAsync(_org, Evening)).Created.ShouldBe(0);
    }

    [Fact]
    public async Task Open_tasks_are_marked_missed_once_their_site_day_ends()
    {
        await AddScheduleAsync(s => s.AllSites = true);
        await _factory.RunTaskGenerationAsync(_org, Evening);
        var done = (await TasksAsync()).First(t => t.SiteId == _london.Id && t.ScheduledDate == March10);
        await Db(async db =>
        {
            (await db.SiteTasks.SingleAsync(t => t.Id == done.Id)).Complete(Evening.UtcDateTime, null, hasFailures: false);
            await db.SaveChangesAsync();
        });
        var openBefore = (await TasksAsync()).Single(t => t.SiteId == _sydney.Id && t.ScheduledDate == March10.AddDays(1));

        // Noon UTC on the 12th is 23:00 in Sydney: London has ended the 10th (done) and 11th, Sydney only the 11th.
        var later = new DateTimeOffset(2026, 3, 12, 12, 0, 0, TimeSpan.Zero);
        (await _factory.RunTaskGenerationAsync(_org, later)).Missed.ShouldBe(2);
        (await _factory.RunTaskGenerationAsync(_org, later)).Missed.ShouldBe(0);

        var tasks = await TasksAsync();
        tasks.Single(t => t.Id == done.Id).Status.ShouldBe(SiteTaskStatus.Completed);
        var missed = tasks.Single(t => t.Id == openBefore.Id);
        missed.Status.ShouldBe(SiteTaskStatus.Missed);
        missed.ConcurrencyStamp.ShouldNotBe(openBefore.ConcurrencyStamp);
        missed.UpdatedAt.ShouldBe(later.UtcDateTime);
        tasks.Where(t => t.SiteId == _london.Id && t.ScheduledDate == March10.AddDays(1)).ShouldHaveSingleItem().Status.ShouldBe(SiteTaskStatus.Missed);
        tasks.Where(t => t.SiteId == _sydney.Id && t.ScheduledDate == March10.AddDays(2)).ShouldHaveSingleItem().Status.ShouldBe(SiteTaskStatus.Open);
        tasks.Where(t => t.ScheduledDate >= new DateOnly(2026, 3, 12) && t.SiteId == _london.Id)
            .ShouldAllBe(t => t.Status == SiteTaskStatus.Open);
    }

    [Fact]
    public async Task Generation_stays_inside_the_tenant()
    {
        var other = (await _factory.CreateOrganizationAsync("Other")).OrganizationId;
        await AddScheduleAsync(s => s.AllSites = true);

        (await _factory.RunTaskGenerationAsync(other, Evening)).Created.ShouldBe(0);
        (await _factory.WithDbAsync(other, db => db.SiteTasks.CountAsync())).ShouldBe(0);
    }
}
