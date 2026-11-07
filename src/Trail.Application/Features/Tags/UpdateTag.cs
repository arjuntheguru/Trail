using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Tags;

public sealed record UpdateTagCommand(string Name, string? Color);

internal sealed class UpdateTagValidator : AbstractValidator<UpdateTagCommand>
{
    public UpdateTagValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Tag.NameMaxLength);
        RuleFor(x => x.Color).Matches(TagRules.ColorPattern).When(x => x.Color is not null)
            .WithMessage("Color must be a hex value such as #1F8A70.");
    }
}

public sealed class UpdateTagHandler(IAppDbContext db)
{
    private static readonly Error NameTaken = Error.Conflict("tag.name_taken", "A tag with this name already exists.");

    public async Task<Result<TagResponse>> Handle(Guid id, UpdateTagCommand command, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null) return Error.NotFound("tag.not_found", "Tag not found.");

        var name = command.Name.Trim();
        if (await db.Tags.AnyAsync(t => t.Name == name && t.Id != id, ct)) return NameTaken;

        tag.Name = name;
        tag.Color = command.Color;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (DbErrors.IsUniqueViolation(ex))
        {
            return NameTaken;
        }

        return new TagResponse(tag.Id, tag.Name, tag.Color);
    }
}
