using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Templates;

/// <summary>Deactivated templates cannot be scheduled and generate no new scheduled tasks.</summary>
public sealed class SetTemplateActiveHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, bool isActive, CancellationToken ct)
    {
        var template = await db.TaskTemplates.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Error.NotFound("template.not_found", "Template not found.");

        template.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
