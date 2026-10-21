using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

public sealed record UpdateTemplateCommand(
    string Name,
    string? Description,
    TaskKind Kind,
    IReadOnlyList<TemplateField> Fields,
    bool RequiresApproval,
    bool RequiresPhoto,
    IReadOnlyList<Guid>? TagIds) : ITemplateInput;

internal sealed class UpdateTemplateValidator : TemplateInputValidator<UpdateTemplateCommand>;

public sealed class UpdateTemplateHandler(IAppDbContext db, TimeProvider clock)
{
    public async Task<Result<TemplateResponse>> Handle(Guid id, UpdateTemplateCommand command, CancellationToken ct)
    {
        var template = await db.TaskTemplates.Include(t => t.Tags).FirstOrDefaultAsync(t => t.Id == id, ct);
        if (template is null) return Error.NotFound("template.not_found", "Template not found.");

        var (error, tagIds, name) = await TemplateRules.CheckAsync(db, command, id, ct);
        if (error is not null) return error;

        // The version marks a change to what is asked, so renames and tag edits leave it alone.
        if (!TemplateRules.FieldsEqual(template.Fields, command.Fields)) template.Version++;
        template.Name = name;
        template.Description = command.Description?.Trim().NullIfEmpty();
        template.Kind = command.Kind;
        template.Fields = [.. command.Fields];
        template.RequiresApproval = command.RequiresApproval;
        template.RequiresPhoto = command.RequiresPhoto;

        // Diff rather than replace: a delete and insert of the same (template, tag) pair would trip the unique index.
        foreach (var stale in template.Tags.Where(x => !tagIds.Contains(x.TagId)).ToList())
        {
            template.Tags.Remove(stale);
            db.TaskTemplateTags.Remove(stale);
        }
        foreach (var added in tagIds.Where(t => template.Tags.All(x => x.TagId != t)).ToList())
            template.Tags.Add(new TaskTemplateTag { TagId = added });

        // Tasks nobody has touched and that are not yet due follow the template; started and past ones are history.
        var now = clock.GetUtcNow().UtcDateTime;
        var untouched = await db.SiteTasks
            .Where(t => t.TemplateId == id && t.Status == SiteTaskStatus.Open && t.DueAt > now && !t.Entries.Any())
            .ToListAsync(ct);
        foreach (var task in untouched)
        {
            task.Title = template.Name;
            task.Description = template.Description;
            task.Kind = template.Kind;
            task.Fields = [.. template.Fields];
            task.RequiresApproval = template.RequiresApproval;
            task.RequiresPhoto = template.RequiresPhoto;
            task.TemplateVersion = template.Version;
        }

        await db.SaveChangesAsync(ct);
        return TemplateMapping.ToResponse(template);
    }
}
