using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Schedules;

public sealed class DeleteScheduleHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var schedule = await ScheduleRules.LoadAsync(db, id, ct);
        if (schedule is null || !ScheduleRules.IsVisible(schedule, scope))
            return Error.NotFound("schedule.not_found", "Schedule not found.");

        // Soft delete keeps the schedule for tasks already generated from it.
        await ScheduleRules.RemoveUntouchedFutureTasksAsync(db, clock, id, ct);
        db.TaskSchedules.Remove(schedule);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
