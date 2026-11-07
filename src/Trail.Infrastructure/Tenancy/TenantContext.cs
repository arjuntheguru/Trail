using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Exceptions;

namespace Trail.Infrastructure.Tenancy;

internal sealed class TenantContext(ICurrentUser currentUser) : ITenantContext
{
    private Guid? _override;

    public Guid? OrganizationId => _override ?? currentUser.OrganizationId;

    public void UseOrganization(Guid organizationId)
    {
        if (organizationId == Guid.Empty)
            throw new ArgumentException("Organization id is required.", nameof(organizationId));

        // A signed-in member may never switch their own scope to someone else's organization.
        if (currentUser.OrganizationId is { } own && own != organizationId && !currentUser.IsPlatformAdmin)
            throw new TenantIsolationException("The current user cannot act in another organization.");

        _override = organizationId;
    }
}
