using System.Security.Claims;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;

namespace Trail.Api.Infrastructure;

internal sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public Guid? UserId => ReadGuid(TrailClaimTypes.UserId);

    public Guid? OrganizationId => ReadGuid(TrailClaimTypes.OrganizationId);

    public bool IsPlatformAdmin => IsAuthenticated && Principal!.HasClaim(TrailClaimTypes.PlatformAdmin, "true");

    public bool HasPermission(string permission) =>
        IsAuthenticated && Principal!.HasClaim(TrailClaimTypes.Permission, permission);

    private Guid? ReadGuid(string claimType) =>
        IsAuthenticated && Guid.TryParse(Principal!.FindFirstValue(claimType), out var value) ? value : null;
}
