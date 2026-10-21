namespace Trail.Application.Common.Security;

/// <summary>Claim names in Trail access tokens. Kept short because they travel on every request.</summary>
public static class TrailClaimTypes
{
    public const string UserId = "sub";
    public const string OrganizationId = "org";
    public const string Permission = "perm";
    public const string PlatformAdmin = "pa";
}
