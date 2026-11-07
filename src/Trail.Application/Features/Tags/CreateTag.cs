using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Tags;

public sealed record CreateTagCommand(string Name, string? Color);

internal static class TagRules
{
    public const string ColorPattern = "^#[0-9A-Fa-f]{6}$";
}

internal sealed class CreateTagValidator : AbstractValidator<CreateTagCommand>
{
    public CreateTagValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Tag.NameMaxLength);
        RuleFor(x => x.Color).Matches(TagRules.ColorPattern).When(x => x.Color is not null)
            .WithMessage("Color must be a hex value such as #1F8A70.");
    }
}

public sealed class CreateTagHandler(IAppDbContext db)
{
    private static readonly Error NameTaken = Error.Conflict("tag.name_taken", "A tag with this name already exists.");

    public async Task<Result<TagResponse>> Handle(CreateTagCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        if (await db.Tags.AnyAsync(t => t.Name == name, ct)) return NameTaken;

        var tag = new Tag { Name = name, Color = command.Color };
        db.Tags.Add(tag);
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
