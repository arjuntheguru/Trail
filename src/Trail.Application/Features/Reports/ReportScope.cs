using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Common;

namespace Trail.Application.Features.Reports;

internal static class ReportScope
{
    public static readonly Error SiteNotFound = Error.NotFound("site.not_found", "Site not found.");

    public const int MaxExportRows = 50_000;

    public static readonly Error TooManyRows = Error.Validation(
        "report.too_many_rows", $"The export would exceed {MaxExportRows:N0} rows. Choose a narrower date range or a single site.");

    /// <summary>A site filter outside the caller's scope (or unknown) is a 404, so ids cannot be probed.</summary>
    public static async Task<Error?> CheckSiteAsync(IAppDbContext db, SiteScope scope, Guid? siteId, CancellationToken ct)
    {
        if (siteId is not { } id) return null;
        if (!scope.Includes(id)) return SiteNotFound;
        // Everything-scope includes ids that do not exist, so confirm the site is in this organization.
        var exists = await db.Sites.AsNoTracking().IgnoreQueryFilters([QueryFilters.SoftDelete]).AnyAsync(s => s.Id == id, ct);
        return exists ? null : SiteNotFound;
    }

    /// <summary>The sites in scope (optionally one), including soft-deleted ones so history keeps its names.</summary>
    public static async Task<List<SiteInfo>> LoadSitesAsync(
        IAppDbContext db, ISiteAccess access, SiteScope scope, Guid? siteId, DateTime nowUtc, bool activeOnly, CancellationToken ct)
    {
        var sites = db.Sites.AsNoTracking().AsQueryable();
        if (!activeOnly) sites = sites.IgnoreQueryFilters([QueryFilters.SoftDelete]);
        else sites = sites.Where(s => s.IsActive);
        sites = access.ApplyTo(sites, scope);
        if (siteId is { } id) sites = sites.Where(s => s.Id == id);

        var rows = await sites.Select(s => new { s.Id, s.Name, s.TimeZone }).ToListAsync(ct);
        return rows.Select(s => new SiteInfo(s.Id, s.Name, SiteTime.Today(nowUtc, SiteTime.Zone(s.TimeZone)))).ToList();
    }

    public static async Task<Dictionary<Guid, string>> UserNamesAsync(IAppDbContext db, IEnumerable<Guid?> ids, CancellationToken ct)
    {
        var distinct = ids.OfType<Guid>().Distinct().ToList();
        if (distinct.Count == 0) return [];
        return await db.Users.AsNoTracking()
            .Where(u => distinct.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);
    }
}
