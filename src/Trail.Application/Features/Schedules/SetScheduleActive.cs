using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Application.Features.Tasks;

namespace Trail.Application.Features.Schedules;

/// <summary>Stopping a schedule withdraws its untouched future tasks; restarting it generates them again.</summary>
public sealed class SetScheduleActiveHandler(IAppDbContext db, ISiteAccess access, TaskGenerator generator, TimeProvider clock)
{
    public async Task<Result> Handle(Guid id, bool isActive, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var schedule = await ScheduleRules.LoadAsync(db, id, ct);
        if (schedule is null || !ScheduleRules.IsVisible(schedule, scope))
            return Error.NotFound("schedule.not_found", "Schedule not found.");

        schedule.IsActive = isActive;
        if (!isActive) await ScheduleRules.RemoveUntouchedFutureTasksAsync(db, clock, id, ct);
        await db.SaveChangesAsync(ct);

        if (isActive) await generator.GenerateAsync(ct, id);
        return Result.Success();
    }
}
