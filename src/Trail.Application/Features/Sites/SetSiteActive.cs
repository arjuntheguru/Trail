using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Sites;

/// <summary>Activates or deactivates a site. Deactivated sites stay visible but should receive no new work.</summary>
public sealed class SetSiteActiveHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result> Handle(Guid id, bool isActive, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var site = await access.ApplyTo(db.Sites, scope).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (site is null) return Error.NotFound("site.not_found", "Site not found.");

        site.IsActive = isActive;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
