using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Regions;

public sealed record UpdateRegionCommand(string Name, string? Description);

internal sealed class UpdateRegionValidator : AbstractValidator<UpdateRegionCommand>
{
    public UpdateRegionValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Region.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(Region.DescriptionMaxLength);
    }
}

public sealed class UpdateRegionHandler(IAppDbContext db)
{
    public async Task<Result<RegionResponse>> Handle(Guid id, UpdateRegionCommand command, CancellationToken ct)
    {
        var region = await db.Regions.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (region is null) return Error.NotFound("region.not_found", "Region not found.");

        var name = command.Name.Trim();
        if (await db.Regions.AnyAsync(r => r.Name == name && r.Id != id, ct))
            return Error.Conflict("region.name_taken", "A region with this name already exists.");

        region.Name = name;
        region.Description = command.Description?.Trim().NullIfEmpty();
        await db.SaveChangesAsync(ct);
        return new RegionResponse(region.Id, region.Name, region.Description);
    }
}
