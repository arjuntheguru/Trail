using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Sites;

public sealed record CreateSiteCommand(
    string Name,
    string? Code,
    Guid? RegionId,
    string? TimeZone,
    AddressDto? Address,
    string? Phone,
    string? Email,
    IReadOnlyList<OpeningHoursDto>? OpeningHours) : ISiteInput;

internal sealed class CreateSiteValidator : SiteInputValidator<CreateSiteCommand>;

public sealed class CreateSiteHandler(IAppDbContext db, ISiteAccess access, ITenantContext tenant)
{
    public async Task<Result<SiteResponse>> Handle(CreateSiteCommand command, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);

        // Someone limited to some regions may only add sites inside them, otherwise they could create a site
        // they then could not see, or place one into a region that is not theirs.
        if (!scope.AllSites && (command.RegionId is not { } regionId || !scope.RegionIds.Contains(regionId)))
            return Error.Forbidden("site.region_out_of_scope", "You can only create sites in regions assigned to you.");

        string? regionName = null;
        if (command.RegionId is { } requestedRegion)
        {
            regionName = await db.Regions.Where(r => r.Id == requestedRegion).Select(r => r.Name).FirstOrDefaultAsync(ct);
            if (regionName is null)
                return Error.Validation("site.unknown_region", "The region does not exist.");
        }

        var name = command.Name.Trim();
        if (await db.Sites.AnyAsync(s => s.Name == name, ct))
            return Error.Conflict("site.name_taken", "A site with this name already exists.");

        var timeZone = command.TimeZone
            ?? await db.Organizations.Where(o => o.Id == tenant.OrganizationId).Select(o => o.DefaultTimeZone).FirstAsync(ct);

        var site = new Site
        {
            Name = name,
            Code = command.Code?.Trim().NullIfEmpty(),
            RegionId = command.RegionId,
            TimeZone = timeZone,
            Address = SiteMapping.ToDomain(command.Address),
            Phone = command.Phone?.Trim().NullIfEmpty(),
            Email = command.Email?.Trim().NullIfEmpty(),
            OpeningHours = SiteMapping.ToDomain(command.OpeningHours),
        };
        db.Sites.Add(site);
        await db.SaveChangesAsync(ct);
        return SiteMapping.ToResponse(site, regionName);
    }
}
