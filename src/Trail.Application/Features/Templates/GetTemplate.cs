using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Templates;

public sealed class GetTemplateHandler(IAppDbContext db)
{
    public async Task<Result<TemplateResponse>> Handle(Guid id, CancellationToken ct)
    {
        var template = await db.TaskTemplates.AsNoTracking().Include(t => t.Tags).FirstOrDefaultAsync(t => t.Id == id, ct);
        return template is null
            ? Error.NotFound("template.not_found", "Template not found.")
            : TemplateMapping.ToResponse(template);
    }
}
