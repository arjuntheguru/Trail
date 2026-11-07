namespace Trail.Application.Common.Abstractions;

/// <summary>The caller of the current request, as established by authentication.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }

    /// <summary>The organization the caller is acting in, from their token. Null for anonymous and platform-only callers.</summary>
    Guid? OrganizationId { get; }

    /// <summary>The caller's membership in <see cref="OrganizationId"/>; decides role and site scope.</summary>
    Guid? MembershipId { get; }

    /// <summary>Platform operator (Trail staff), not a member of any customer organization by default.</summary>
    bool IsPlatformAdmin { get; }

    bool HasPermission(string permission);
}

/// <summary>
/// The organization that data access is scoped to. Defaults to the current user's organization; background
/// jobs set it explicitly with <see cref="UseOrganization"/> before touching tenant data.
/// </summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }

    void UseOrganization(Guid organizationId);
}
