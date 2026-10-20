namespace Trail.Application.Features.Auth;

public sealed record UserSummary(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    bool EmailConfirmed,
    bool IsPlatformAdmin);

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, Guid MembershipId, string Role);

/// <summary>
/// A signed-in session. The access token is short-lived; exchange the refresh token at /auth/refresh before it
/// expires. Each refresh token works once.
/// </summary>
public sealed record AuthSessionResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserSummary User,
    OrganizationSummary? Organization,
    IReadOnlyList<OrganizationSummary> Organizations,
    IReadOnlyList<string> Permissions);
