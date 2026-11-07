using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Organizations;

public sealed class GetCurrentOrganizationHandler(IAppDbContext db, ICurrentUser user)
{
    public async Task<Result<OrganizationResponse>> Handle(CancellationToken ct)
    {
        // Organization is not tenant-filtered; the id comes from the caller's token, never from the client.
        var organization = await db.Organizations.AsNoTracking()
            .Where(o => o.Id == user.OrganizationId)
            .Select(o => new OrganizationResponse(o.Id, o.Name, o.Slug, o.DefaultTimeZone))
            .FirstOrDefaultAsync(ct);

        return organization is null
            ? Error.NotFound("organization.not_found", "Organization not found.")
            : organization;
    }
}
