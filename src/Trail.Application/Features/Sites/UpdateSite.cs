using Microsoft.EntityFrameworkCore;
using Trail.Application.Common;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Sites;

/// <summary>Replaces the site's details. A null <see cref="TimeZone"/> keeps the current one.</summary>
public sealed record UpdateSiteCommand(
    string Name,
    string? Code,
    Guid? RegionId,
    string? TimeZone,
    AddressDto? Address,
    string? Phone,
    string? Email,
    IReadOnlyList<OpeningHoursDto>? OpeningHours) : ISiteInput;

internal sealed class UpdateSiteValidator : SiteInputValidator<UpdateSiteCommand>;

public sealed class UpdateSiteHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result<SiteResponse>> Handle(Guid id, UpdateSiteCommand command, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var site = await access.ApplyTo(db.Sites, scope).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (site is null) return Error.NotFound("site.not_found", "Site not found.");

        // Moving a site is only allowed into one of the caller's own regions.
        if (!scope.AllSites && command.RegionId != site.RegionId
            && (command.RegionId is not { } target || !scope.RegionIds.Contains(target)))
            return Error.Forbidden("site.region_out_of_scope", "You can only move sites into regions assigned to you.");

        if (command.RegionId is { } regionId && !await db.Regions.AnyAsync(r => r.Id == regionId, ct))
            return Error.Validation("site.unknown_region", "The region does not exist.");

        var name = command.Name.Trim();
        if (await db.Sites.AnyAsync(s => s.Name == name && s.Id != id, ct))
            return Error.Conflict("site.name_taken", "A site with this name already exists.");

        site.Name = name;
        site.Code = command.Code?.Trim().NullIfEmpty();
        site.RegionId = command.RegionId;
        site.TimeZone = command.TimeZone ?? site.TimeZone;
        site.Address = SiteMapping.ToDomain(command.Address);
        site.Phone = command.Phone?.Trim().NullIfEmpty();
        site.Email = command.Email?.Trim().NullIfEmpty();
        site.OpeningHours = SiteMapping.ToDomain(command.OpeningHours);
        await db.SaveChangesAsync(ct);

        var regionName = site.RegionId is { } rid
            ? await db.Regions.Where(r => r.Id == rid).Select(r => r.Name).FirstOrDefaultAsync(ct)
            : null;
        return SiteMapping.ToResponse(site, regionName);
    }
}
