using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Schedules;

public sealed class GetScheduleHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<ScheduleResponse>> Handle(Guid id, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var schedule = await ScheduleRules.LoadAsync(db, id, ct);

        // Schedules reaching beyond the caller's sites look exactly like schedules that do not exist.
        return schedule is null || !ScheduleRules.IsVisible(schedule, scope)
            ? Error.NotFound("schedule.not_found", "Schedule not found.")
            : ScheduleMapping.ToResponse(schedule);
    }
}
