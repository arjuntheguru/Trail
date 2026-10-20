using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Regions;

public sealed record CreateRegionCommand(string Name, string? Description);

internal sealed class CreateRegionValidator : AbstractValidator<CreateRegionCommand>
{
    public CreateRegionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Region.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(Region.DescriptionMaxLength);
    }
}

public sealed class CreateRegionHandler(IAppDbContext db)
{
    public async Task<Result<RegionResponse>> Handle(CreateRegionCommand command, CancellationToken ct)
    {
        var name = command.Name.Trim();
        if (await db.Regions.AnyAsync(r => r.Name == name, ct))
            return Error.Conflict("region.name_taken", "A region with this name already exists.");

        var region = new Region { Name = name, Description = command.Description?.Trim().NullIfEmpty() };
        db.Regions.Add(region);
        await db.SaveChangesAsync(ct);
        return new RegionResponse(region.Id, region.Name, region.Description);
    }
}
