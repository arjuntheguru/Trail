using Trail.Domain.Common;

namespace Trail.Domain.Integrations;

public static class ApiKeyScopes
{
    public const string SensorsWrite = "sensors.write";

    public static IReadOnlyList<string> All { get; } = [SensorsWrite];

    public static bool IsKnown(string scope) => All.Contains(scope, StringComparer.Ordinal);
}

/// <summary>
/// A credential for machines (sensors, scripts) acting in one organization with a narrow set of scopes. Only the
/// SHA-256 hash of the key is stored; the key itself is shown once when it is created.
/// </summary>
public sealed class ApiKey : TenantEntity, ISoftDeletable
{
    public const int NameMaxLength = 100;
    public const int PrefixLength = 8;
    public const int MaxPerOrganization = 50;
    public const int MaxSites = 200;

    public required string Name { get; set; }

    /// <summary>The identifying part of the key (the 8 characters after "trl_"), safe to show in lists.</summary>
    public required string Prefix { get; set; }

    /// <summary>Lowercase hex SHA-256 of the whole key. Globally unique because the key carries 190 bits of randomness.</summary>
    [NotAudited]
    public required string KeyHash { get; set; }

    public HashSet<string> Scopes { get; set; } = [];

    /// <summary>Restricts the key to these sites. Null or empty means every site of the organization.</summary>
    public List<Guid>? SiteIds { get; set; }

    public DateTime? ExpiresAt { get; set; }

    [NotAudited]
    public DateTime? LastUsedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    public bool IsUsable(DateTime now) => RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);

    public bool AllowsSite(Guid siteId) => SiteIds is not { Count: > 0 } || SiteIds.Contains(siteId);

    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedById { get; set; }
}
