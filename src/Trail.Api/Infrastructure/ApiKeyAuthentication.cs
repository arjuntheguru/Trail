using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Application.Features.Integrations;

namespace Trail.Api.Infrastructure;

public static class ApiKeyAuth
{
    public const string Scheme = "ApiKey";

    /// <summary>Requires the ApiKey scheme (and only that one) with the sensors.write scope.</summary>
    public const string SensorsWritePolicy = "sensors_write";

    public const string HeaderName = "X-Api-Key";

    /// <summary>The key from <c>Authorization: ApiKey {key}</c> or <c>X-Api-Key</c>, or null when none was presented.</summary>
    public static string? ReadKey(HttpRequest request)
    {
        var authorization = request.Headers.Authorization.ToString();
        const string prefix = "ApiKey ";
        if (authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return authorization[prefix.Length..].Trim();

        var header = request.Headers[HeaderName].ToString();
        return string.IsNullOrWhiteSpace(header) ? null : header.Trim();
    }
}

/// <summary>
/// Authenticates machine callers by organization API key. Only the sensors endpoints use this scheme; the default
/// scheme stays JWT, so keys cannot call anything else and tokens cannot call the sensors endpoints.
/// </summary>
internal sealed class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IAppDbContext db, TimeProvider clock)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>LastUsedAt is written at most this often per key, so busy sensors do not turn every request into a write.</summary>
    private static readonly TimeSpan TouchInterval = TimeSpan.FromMinutes(1);

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var presented = ApiKeyAuth.ReadKey(Request);
        if (presented is null) return AuthenticateResult.NoResult();
        if (!ApiKeyFormat.TryParse(presented, out _)) return AuthenticateResult.Fail("Invalid API key.");

        var hash = ApiKeyFormat.Hash(presented);
        var ct = Context.RequestAborted;

        // The caller's organization is not known until the key identifies it, so the tenant filter cannot apply to this
        // one lookup (it would match no rows). It is safe: the key is found only by the hash of a secret that cannot
        // be guessed, and the organization the key belongs to then becomes the request's tenant. The soft-delete
        // filter stays, so deleted keys do not authenticate.
        var key = await db.ApiKeys
            .IgnoreQueryFilters([QueryFilters.Tenant])
            .AsNoTracking()
            .Where(k => k.KeyHash == hash)
            .Select(k => new { k.Id, k.OrganizationId, k.KeyHash, k.Scopes, k.ExpiresAt, k.RevokedAt, k.LastUsedAt })
            .FirstOrDefaultAsync(ct);

        if (key is null || !ApiKeyFormat.Matches(presented, key.KeyHash)) return AuthenticateResult.Fail("Invalid API key.");

        var now = clock.GetUtcNow().UtcDateTime;
        if (key.RevokedAt is not null || (key.ExpiresAt is { } expires && expires <= now))
            return AuthenticateResult.Fail("The API key is revoked or expired.");

        // Organization is not tenant-owned, so no filter is involved; soft-deleted or deactivated organizations are refused.
        if (!await db.Organizations.AsNoTracking().AnyAsync(o => o.Id == key.OrganizationId && o.IsActive, ct))
            return AuthenticateResult.Fail("The organization is not active.");

        if (key.LastUsedAt is null || now - key.LastUsedAt >= TouchInterval)
            await TouchAsync(key.Id, now, ct);

        var claims = new List<Claim>
        {
            new(TrailClaimTypes.OrganizationId, key.OrganizationId.ToString()),
            new(ApiKeyClaims.ApiKeyId, key.Id.ToString()),
        };
        claims.AddRange(key.Scopes.Select(scope => new Claim(ApiKeyClaims.Scope, scope)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
    }

    private async Task TouchAsync(Guid keyId, DateTime now, CancellationToken ct)
    {
        try
        {
            // Bulk update: bookkeeping, not a change worth auditing, and no tenant is active yet. The condition
            // keeps concurrent requests from all writing.
            var cutoff = now - TouchInterval;
            await db.ApiKeys
                .IgnoreQueryFilters([QueryFilters.Tenant])
                .Where(k => k.Id == keyId && (k.LastUsedAt == null || k.LastUsedAt < cutoff))
                .ExecuteUpdateAsync(s => s.SetProperty(k => k.LastUsedAt, now), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Logger.LogWarning(ex, "Could not record the last use of API key {ApiKeyId}.", keyId);
        }
    }
}
