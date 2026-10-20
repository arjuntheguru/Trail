using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Common;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

public sealed record SnoozeTaskCommand(DateOnly Date);

internal sealed class SnoozeTaskValidator : AbstractValidator<SnoozeTaskCommand>
{
    public SnoozeTaskValidator() => RuleFor(x => x.Date).NotEqual(default(DateOnly)).WithMessage("A date is required.");
}

/// <summary>Moves an open task to a later day, at most <see cref="SiteTask.MaxSnoozeDays"/> after the site's today.</summary>
public sealed class SnoozeTaskHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, SnoozeTaskCommand command, CancellationToken ct)
    {
        var task = await TaskRules.FindAsync(db, access, id, ct);
        if (task is null) return TaskRules.NotFound;

        if (task.Status != SiteTaskStatus.Open)
            return Error.Conflict("task.not_open", "Only open tasks can be snoozed.");

        var now = clock.GetUtcNow().UtcDateTime;
        var timeZone = await db.Sites.AsNoTracking().IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(s => s.Id == task.SiteId).Select(s => s.TimeZone).FirstAsync(ct);
        var zone = SiteTime.Zone(timeZone);
        var latest = SiteTime.Today(now, zone).AddDays(SiteTask.MaxSnoozeDays);

        if (command.Date <= task.DueDate)
            return Error.Validation("task.invalid_snooze_date", "Snooze to a day after the current due date.");
        if (command.Date > latest)
            return Error.Validation("task.invalid_snooze_date", $"A task can be snoozed at most {SiteTask.MaxSnoozeDays} days ahead.");

        task.Snooze(command.Date, zone);
        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}
