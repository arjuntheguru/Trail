using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Schedules;

/// <summary>The fields shared by creating and updating a schedule.</summary>
public interface IScheduleInput
{
    Guid TemplateId { get; }
    Frequency Frequency { get; }
    int Interval { get; }
    DateOnly StartDate { get; }
    DateOnly? EndDate { get; }
    WeekDays Days { get; }
    int? DayOfMonth { get; }
    TimeOnly? DueTime { get; }
    TimeOnly? AvailableFrom { get; }
    bool AllSites { get; }
    IReadOnlyList<Guid>? SiteIds { get; }
    IReadOnlyList<Guid>? RegionIds { get; }
    Guid? DepartmentId { get; }
}

internal abstract class ScheduleInputValidator<T> : AbstractValidator<T> where T : IScheduleInput
{
    protected ScheduleInputValidator()
    {
        RuleFor(x => x.TemplateId).NotEmpty();
        RuleFor(x => x.Frequency).IsInEnum();
        RuleFor(x => x.DueTime).NotNull();
        RuleFor(x => x.AvailableFrom).LessThan(x => x.DueTime)
            .When(x => x.AvailableFrom is not null && x.DueTime is not null)
            .WithMessage("'Available from' must be earlier than the due time.");
        RuleFor(x => x).Custom((x, context) =>
        {
            foreach (var message in ScheduleRules.ToRecurrence(x).Validate())
                context.AddFailure("recurrence", message);
        });
    }
}

internal static class ScheduleRules
{
    public static Recurrence ToRecurrence(IScheduleInput i) => new()
    {
        Frequency = i.Frequency, Interval = i.Interval, StartDate = i.StartDate, EndDate = i.EndDate,
        Days = i.Days, DayOfMonth = i.DayOfMonth,
    };

    /// <summary>
    /// A caller limited to some sites sees a schedule only when every one of its targets is theirs. A schedule for
    /// all sites, or for a region they do not hold, would also run on sites they cannot reach.
    /// </summary>
    public static bool IsVisible(TaskSchedule schedule, SiteScope scope) =>
        scope.AllSites
        || (!schedule.AllSites
            && schedule.Sites.All(x => scope.Includes(x.SiteId))
            && schedule.Regions.All(x => scope.RegionIds.Contains(x.RegionId)));

    /// <summary>Validates references and scope. On success returns the template and the de-duplicated targets.</summary>
    public static async Task<(Error? Error, TaskTemplate? Template, List<Guid> SiteIds, List<Guid> RegionIds)> CheckAsync(
        IAppDbContext db, ISiteAccess access, IScheduleInput input, CancellationToken ct)
    {
        var template = await db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == input.TemplateId, ct);
        if (template is null) return Fail(Error.Validation("schedule.unknown_ids", "The template does not exist."));
        if (!template.IsActive) return Fail(Error.Validation("schedule.template_inactive", "The template is not active."));

        var siteIds = input.AllSites ? [] : (input.SiteIds ?? []).Distinct().ToList();
        var regionIds = input.AllSites ? [] : (input.RegionIds ?? []).Distinct().ToList();
        if (!input.AllSites && siteIds.Count == 0 && regionIds.Count == 0)
            return Fail(Error.Validation("schedule.no_target", "Choose all sites, or at least one site or region."));

        if ((siteIds.Count > 0 && await db.Sites.CountAsync(s => siteIds.Contains(s.Id), ct) != siteIds.Count)
            || (regionIds.Count > 0 && await db.Regions.CountAsync(r => regionIds.Contains(r.Id), ct) != regionIds.Count)
            || (input.DepartmentId is { } dept && !await db.Departments.AnyAsync(d => d.Id == dept, ct)))
            return Fail(Error.Validation("schedule.unknown_ids", "One or more sites, regions or the department do not exist."));

        var scope = await access.GetScopeAsync(ct);
        if (!scope.AllSites
            && (input.AllSites || siteIds.Any(id => !scope.Includes(id)) || regionIds.Any(id => !scope.RegionIds.Contains(id))))
            return Fail(Error.Forbidden("schedule.out_of_scope", "You can only schedule tasks for sites and regions assigned to you."));

        return (null, template, siteIds, regionIds);

        static (Error?, TaskTemplate?, List<Guid>, List<Guid>) Fail(Error error) => (error, null, [], []);
    }

    /// <summary>Copies the input onto the schedule, changing only the target rows that differ (unique indexes).</summary>
    public static void Apply(
        IAppDbContext db, TaskSchedule schedule, IScheduleInput input, List<Guid> siteIds, List<Guid> regionIds)
    {
        schedule.TemplateId = input.TemplateId;
        schedule.Frequency = input.Frequency;
        schedule.Interval = input.Interval;
        schedule.StartDate = input.StartDate;
        schedule.EndDate = input.EndDate;
        schedule.Days = input.Days;
        schedule.DayOfMonth = input.DayOfMonth;
        schedule.DueTime = input.DueTime!.Value;
        schedule.AvailableFrom = input.AvailableFrom;
        schedule.AllSites = input.AllSites;
        schedule.DepartmentId = input.DepartmentId;

        foreach (var stale in schedule.Sites.Where(x => !siteIds.Contains(x.SiteId)).ToList())
        {
            schedule.Sites.Remove(stale);
            db.TaskScheduleSites.Remove(stale);
        }
        foreach (var id in siteIds.Where(id => schedule.Sites.All(x => x.SiteId != id)).ToList())
            schedule.Sites.Add(new TaskScheduleSite { SiteId = id });

        foreach (var stale in schedule.Regions.Where(x => !regionIds.Contains(x.RegionId)).ToList())
        {
            schedule.Regions.Remove(stale);
            db.TaskScheduleRegions.Remove(stale);
        }
        foreach (var id in regionIds.Where(id => schedule.Regions.All(x => x.RegionId != id)).ToList())
            schedule.Regions.Add(new TaskScheduleRegion { RegionId = id });
    }

    /// <summary>
    /// Removes the schedule's tasks nobody has touched and that are not yet due, so a changed or stopped schedule
    /// leaves no stale work on lists. This is a real delete (tasks are not soft-deletable); started, snoozed,
    /// completed and past tasks are history and stay. Must be saved before regenerating, because of the
    /// unique (schedule, site, date) index.
    /// </summary>
    public static async Task RemoveUntouchedFutureTasksAsync(IAppDbContext db, TimeProvider clock, Guid scheduleId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var tasks = await db.SiteTasks
            .Where(t => t.ScheduleId == scheduleId && t.Status == SiteTaskStatus.Open && t.SnoozeCount == 0
                        && t.DueAt > now && !t.Entries.Any())
            .ToListAsync(ct);
        db.SiteTasks.RemoveRange(tasks);
    }

    public static Task<TaskSchedule?> LoadAsync(IAppDbContext db, Guid id, CancellationToken ct) =>
        db.TaskSchedules.Include(s => s.Template).Include(s => s.Sites).Include(s => s.Regions)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
}
