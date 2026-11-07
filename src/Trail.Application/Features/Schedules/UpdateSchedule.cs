using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Tasks;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Schedules;

public sealed record UpdateScheduleCommand(
    Guid TemplateId,
    Frequency Frequency,
    DateOnly StartDate,
    TimeOnly? DueTime,
    int Interval = 1,
    DateOnly? EndDate = null,
    WeekDays Days = WeekDays.None,
    int? DayOfMonth = null,
    TimeOnly? AvailableFrom = null,
    bool AllSites = false,
    IReadOnlyList<Guid>? SiteIds = null,
    IReadOnlyList<Guid>? RegionIds = null,
    Guid? DepartmentId = null) : IScheduleInput;

internal sealed class UpdateScheduleValidator : ScheduleInputValidator<UpdateScheduleCommand>;

public sealed class UpdateScheduleHandler(IAppDbContext db, ISiteAccess access, TaskGenerator generator, TimeProvider clock)
{
    public async Task<Result<ScheduleResponse>> Handle(Guid id, UpdateScheduleCommand command, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var schedule = await ScheduleRules.LoadAsync(db, id, ct);
        if (schedule is null || !ScheduleRules.IsVisible(schedule, scope))
            return Error.NotFound("schedule.not_found", "Schedule not found.");

        var (error, template, siteIds, regionIds) = await ScheduleRules.CheckAsync(db, access, command, ct);
        if (error is not null) return error;

        ScheduleRules.Apply(db, schedule, command, siteIds, regionIds);
        schedule.Template = template!;
        await ScheduleRules.RemoveUntouchedFutureTasksAsync(db, clock, id, ct);
        await db.SaveChangesAsync(ct);

        if (schedule.IsActive) await generator.GenerateAsync(ct, id);
        return ScheduleMapping.ToResponse(schedule);
    }
}
