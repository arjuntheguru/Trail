using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Auth;

public sealed record ActiveMembership(Guid MembershipId, Guid OrganizationId, string OrganizationName, string OrganizationSlug,
    string RoleName, IReadOnlyList<string> Permissions)
{
    public OrganizationSummary ToSummary() => new(OrganizationId, OrganizationName, OrganizationSlug, MembershipId, RoleName);
}

/// <summary>Issues access + refresh tokens. Shared by every flow that signs a user in.</summary>
public sealed class SessionIssuer(
    IAppDbContext db,
    IAccessTokenIssuer tokens,
    IRequestContext request,
    TimeProvider clock)
{
    /// <summary>
    /// Active memberships of a user in active organizations. Deliberately ignores the tenant filter: sign-in
    /// happens before any organization is chosen, and the query is restricted to the user's own rows.
    /// </summary>
    public async Task<IReadOnlyList<ActiveMembership>> ActiveMembershipsAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.Memberships
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .Where(m => m.UserId == userId && m.Status == MembershipStatus.Active)
            .Join(db.Organizations.Where(o => o.IsActive), m => m.OrganizationId, o => o.Id,
                (m, o) => new { m.Id, OrganizationId = o.Id, o.Name, o.Slug, RoleName = m.Role.Name, m.Role.Permissions })
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return rows.Select(r => new ActiveMembership(r.Id, r.OrganizationId, r.Name, r.Slug, r.RoleName,
                r.Permissions.Where(Common.Security.Permissions.IsKnown).ToList()))
            .ToList();
    }

    /// <summary>
    /// Adds a refresh token row and returns the session. Pass <paramref name="familyId"/> when rotating so the
    /// chain stays linkable for reuse detection. The caller saves.
    /// </summary>
    public AuthSessionResponse Issue(User user, ActiveMembership? active, IReadOnlyList<ActiveMembership> all, Guid? familyId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var permissions = active?.Permissions ?? [];

        var access = tokens.Issue(new AccessTokenSubject(
            user.Id, user.Email, user.FullName, user.IsPlatformAdmin, active?.OrganizationId, active?.MembershipId, permissions));

        var (refreshToken, hash) = SecretToken.Create();
        var refresh = new RefreshToken
        {
            UserId = user.Id,
            OrganizationId = active?.OrganizationId,
            FamilyId = familyId ?? Guid.CreateVersion7(),
            TokenHash = hash,
            SecurityStamp = user.SecurityStamp,
            CreatedAt = now,
            ExpiresAt = now + tokens.RefreshTokenLifetime,
            CreatedByIp = request.IpAddress,
            UserAgent = request.UserAgent,
        };
        db.RefreshTokens.Add(refresh);

        return new AuthSessionResponse(
            access.Token,
            access.ExpiresAt,
            refreshToken,
            refresh.ExpiresAt,
            ToSummary(user),
            active?.ToSummary(),
            all.Select(m => m.ToSummary()).ToList(),
            permissions);
    }

    public Task RevokeAllAsync(Guid userId, CancellationToken ct, Guid? organizationId = null)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .Where(t => organizationId == null || t.OrganizationId == organizationId)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public Task RevokeFamilyAsync(Guid familyId, CancellationToken ct)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    public static UserSummary ToSummary(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.EmailConfirmedAt is not null, user.IsPlatformAdmin);
}
