using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

public sealed record CreateTemplateCommand(
    string Name,
    string? Description,
    TaskKind Kind,
    IReadOnlyList<TemplateField> Fields,
    bool RequiresApproval,
    bool RequiresPhoto,
    IReadOnlyList<Guid>? TagIds) : ITemplateInput;

internal sealed class CreateTemplateValidator : TemplateInputValidator<CreateTemplateCommand>;

public sealed class CreateTemplateHandler(IAppDbContext db)
{
    public async Task<Result<TemplateResponse>> Handle(CreateTemplateCommand command, CancellationToken ct)
    {
        var (error, tagIds, name) = await TemplateRules.CheckAsync(db, command, null, ct);
        if (error is not null) return error;

        var template = new TaskTemplate
        {
            Name = name,
            Description = command.Description?.Trim().NullIfEmpty(),
            Kind = command.Kind,
            Fields = [.. command.Fields],
            RequiresApproval = command.RequiresApproval,
            RequiresPhoto = command.RequiresPhoto,
            Tags = [.. tagIds.Select(id => new TaskTemplateTag { TagId = id })],
        };
        db.TaskTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return TemplateMapping.ToResponse(template);
    }
}
