using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Sites;

public sealed class DeleteSiteHandler(IAppDbContext db, ISiteAccess access)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var site = await access.ApplyTo(db.Sites, scope).FirstOrDefaultAsync(s => s.Id == id, ct);
        if (site is null) return Error.NotFound("site.not_found", "Site not found.");

        // Soft delete: the interceptor marks the row deleted so history that refers to the site survives.
        // Per-member access rows for it stay, but grant nothing because deleted sites are filtered out.
        db.Sites.Remove(site);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
