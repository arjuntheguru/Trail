using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Security;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Organizations;

/// <summary>
/// Creates a new organization with its system roles and an Owner membership for <paramref name="owner"/>.
/// Switches the tenant context to the new organization, so call it from a scope that is not otherwise tenant-bound.
/// Does not save; the caller commits everything in one unit of work.
/// </summary>
public sealed partial class OrganizationProvisioner(IAppDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public async Task<(Organization Organization, Membership OwnerMembership)> ProvisionAsync(
        string name, User owner, string? timeZone, CancellationToken ct)
    {
        var organization = new Organization
        {
            Name = name.Trim(),
            Slug = await UniqueSlugAsync(name, ct),
            DefaultTimeZone = timeZone ?? "UTC",
        };
        db.Organizations.Add(organization);
        tenant.UseOrganization(organization.Id);

        Role? ownerRole = null;
        foreach (var (roleName, permissions) in SystemRoles.Definitions)
        {
            var role = new Role { Name = roleName, IsSystem = true, Permissions = [.. permissions] };
            db.Roles.Add(role);
            if (roleName == SystemRoles.Owner) ownerRole = role;
        }

        var membership = new Membership
        {
            User = owner,
            UserId = owner.Id,
            Role = ownerRole!,
            RoleId = ownerRole!.Id,
            Status = MembershipStatus.Active,
            AllSites = true,
            JoinedAt = clock.GetUtcNow().UtcDateTime,
        };
        db.Memberships.Add(membership);

        return (organization, membership);
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = NonSlugCharacters().Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');
        if (baseSlug.Length == 0) baseSlug = "org";
        if (baseSlug.Length > 48) baseSlug = baseSlug[..48].TrimEnd('-');

        // Soft-deleted organizations keep their slug reserved.
        var taken = await db.Organizations.IgnoreQueryFilters([QueryFilters.SoftDelete])
            .Where(o => o.Slug == baseSlug || o.Slug.StartsWith(baseSlug + "-"))
            .Select(o => o.Slug)
            .ToListAsync(ct);

        if (!taken.Contains(baseSlug)) return baseSlug;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugCharacters();
}
