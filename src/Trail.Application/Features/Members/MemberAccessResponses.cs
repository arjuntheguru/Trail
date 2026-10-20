namespace Trail.Application.Features.Members;

/// <summary>
/// What a member can reach. With <see cref="AllSites"/> the site and region lists are ignored for scoping
/// but are kept, so switching AllSites off later restores them.
/// </summary>
public sealed record MemberAccessResponse(
    bool AllSites,
    IReadOnlyList<Guid> SiteIds,
    IReadOnlyList<Guid> RegionIds,
    IReadOnlyList<Guid> DepartmentIds);
