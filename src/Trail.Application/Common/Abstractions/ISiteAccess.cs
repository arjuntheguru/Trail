using Trail.Domain.Organizations;

namespace Trail.Application.Common.Abstractions;

/// <summary>
/// The sites a caller may work with. <see cref="SiteIds"/> already contains the live sites of every assigned
/// region, so a site is reachable if and only if <see cref="Includes"/> says so.
/// </summary>
public sealed record SiteScope(bool AllSites, IReadOnlySet<Guid> SiteIds, IReadOnlySet<Guid> RegionIds)
{
    public static SiteScope Everything { get; } = new(true, new HashSet<Guid>(), new HashSet<Guid>());

    public static SiteScope Nothing { get; } = new(false, new HashSet<Guid>(), new HashSet<Guid>());

    public bool Includes(Guid siteId) => AllSites || SiteIds.Contains(siteId);
}

/// <summary>
/// Decides which sites the current caller can see. Every query over site-scoped data (sites, tasks, issues,
/// reports) must be restricted through this, otherwise members assigned to a subset of sites see everything.
/// </summary>
public interface ISiteAccess
{
    /// <summary>The caller's scope, resolved once per request scope.</summary>
    Task<SiteScope> GetScopeAsync(CancellationToken ct);

    Task<bool> CanAccessSiteAsync(Guid siteId, CancellationToken ct);

    /// <summary>Restricts <paramref name="sites"/> to the sites inside <paramref name="scope"/>.</summary>
    IQueryable<Site> ApplyTo(IQueryable<Site> sites, SiteScope scope);
}
