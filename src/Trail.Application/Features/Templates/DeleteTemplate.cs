using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Templates;

public sealed class DeleteTemplateHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var template = await db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Error.NotFound("template.not_found", "Template not found.");

        if (await db.TaskSchedules.AnyAsync(s => s.TemplateId == id, ct))
            return Error.Conflict("template.in_use", "A schedule still uses this template. Delete the schedule first.");

        // Soft delete: generated tasks keep pointing at the template.
        db.TaskTemplates.Remove(template);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
