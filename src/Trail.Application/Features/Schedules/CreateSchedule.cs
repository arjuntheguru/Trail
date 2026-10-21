using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Tasks;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Schedules;

public sealed record CreateScheduleCommand(
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

internal sealed class CreateScheduleValidator : ScheduleInputValidator<CreateScheduleCommand>;

public sealed class CreateScheduleHandler(IAppDbContext db, ISiteAccess access, TaskGenerator generator)
{
    public async Task<Result<ScheduleResponse>> Handle(CreateScheduleCommand command, CancellationToken ct)
    {
        var (error, template, siteIds, regionIds) = await ScheduleRules.CheckAsync(db, access, command, ct);
        if (error is not null) return error;

        var schedule = new TaskSchedule { TemplateId = template!.Id, Template = template };
        ScheduleRules.Apply(db, schedule, command, siteIds, regionIds);
        db.TaskSchedules.Add(schedule);
        await db.SaveChangesAsync(ct);

        // So today's and tomorrow's tasks are on the lists straight away rather than at the next background run.
        await generator.GenerateAsync(ct, schedule.Id);
        return ScheduleMapping.ToResponse(schedule);
    }
}
