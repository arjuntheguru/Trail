using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Common;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Tasks;

/// <summary>An unscheduled task from a template. Date defaults to the site's today, due time to 23:59.</summary>
public sealed record CreateAdHocTaskCommand(Guid TemplateId, DateOnly? Date = null, TimeOnly? DueTime = null, Guid? DepartmentId = null);

internal sealed class CreateAdHocTaskValidator : AbstractValidator<CreateAdHocTaskCommand>
{
    public CreateAdHocTaskValidator() => RuleFor(x => x.TemplateId).NotEmpty();
}

public sealed class CreateAdHocTaskHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public const int MaxDaysAhead = 30;
    public static readonly TimeOnly DefaultDueTime = new(23, 59);

    public async Task<Result<TaskDetailResponse>> Handle(Guid siteId, CreateAdHocTaskCommand command, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var site = await access.ApplyTo(db.Sites.AsNoTracking(), scope)
            .Where(s => s.Id == siteId)
            .Select(s => new { s.IsActive, s.TimeZone })
            .FirstOrDefaultAsync(ct);
        if (site is null) return Error.NotFound("site.not_found", "Site not found.");
        if (!site.IsActive) return Error.Conflict("site.inactive", "The site is not active.");

        var template = await db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == command.TemplateId, ct);
        if (template is null) return Error.Validation("task.unknown_template", "The template does not exist.");
        if (!template.IsActive) return Error.Validation("task.template_inactive", "The template is not active.");

        if (command.DepartmentId is { } departmentId && !await db.Departments.AnyAsync(d => d.Id == departmentId, ct))
            return Error.Validation("task.unknown_department", "The department does not exist.");

        var now = clock.GetUtcNow().UtcDateTime;
        var zone = SiteTime.Zone(site.TimeZone);
        var today = SiteTime.Today(now, zone);
        var date = command.Date ?? today;
        if (date < today || date > today.AddDays(MaxDaysAhead))
            return Error.Validation("task.invalid_date", $"The date must be today or up to {MaxDaysAhead} days ahead.");

        var task = SiteTask.FromTemplate(
            template, siteId, zone, date, command.DueTime ?? DefaultDueTime, availableFrom: null,
            SiteTaskSource.AdHoc, scheduleId: null, command.DepartmentId);
        db.SiteTasks.Add(task);
        await db.SaveChangesAsync(ct);
        return await TaskMapping.ToDetailAsync(db, task, now, ct);
    }
}
