using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Integrations;

/// <summary>
/// Webhooks receive events of every site and API keys without a site list reach every site, so managing them
/// takes organization-wide access; a caller limited to some sites must not see or change them.
/// </summary>
public static class IntegrationAccess
{
    public static Error? RequireAllSites(SiteScope scope) =>
        scope.AllSites
            ? null
            : Error.Forbidden("integrations.all_sites_required", "Managing this requires access to all sites.");

    public static async Task<Error?> RequireAllSitesAsync(ISiteAccess access, CancellationToken ct) =>
        RequireAllSites(await access.GetScopeAsync(ct));
}
