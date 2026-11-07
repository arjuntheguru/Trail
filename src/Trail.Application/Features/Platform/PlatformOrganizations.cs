using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Results;
using Trail.Application.Common.Validation;
using Trail.Application.Features.Members;
using Trail.Application.Features.Organizations;
using Trail.Domain.Identity;
using Trail.Domain.Organizations;

namespace Trail.Application.Features.Platform;

public sealed record PlatformOrganizationResponse(
    Guid Id, string Name, string Slug, string DefaultTimeZone, bool IsActive, DateTime CreatedAt, int MemberCount);

public sealed record CreatePlatformOrganizationCommand(
    string Name, string OwnerEmail, string OwnerFirstName, string OwnerLastName, string? TimeZone = null);

internal sealed class CreatePlatformOrganizationValidator : AbstractValidator<CreatePlatformOrganizationCommand>
{
    public CreatePlatformOrganizationValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(Organization.NameMaxLength);
        RuleFor(x => x.OwnerEmail).Email();
        RuleFor(x => x.OwnerFirstName).PersonName();
        RuleFor(x => x.OwnerLastName).PersonName();
        RuleFor(x => x.TimeZone).TimeZone();
    }
}

/// <summary>
/// Trail staff onboarding customers. Platform admins see organizations but not their data: tenant-owned
/// tables stay filtered, and only membership counts are read across tenants.
/// </summary>
public sealed class PlatformOrganizationsHandler(
    IAppDbContext db,
    OrganizationProvisioner provisioner,
    InvitationSender invitations)
{
    public async Task<PagedResult<PlatformOrganizationResponse>> List(string? search, PageRequest page, CancellationToken ct)
    {
        var organizations = db.Organizations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            organizations = organizations.Where(o => o.Name.Contains(term) || o.Slug.Contains(term));
        }

        // Cross-tenant on purpose: platform staff need member counts for every customer.
        var memberships = db.Memberships.IgnoreQueryFilters([QueryFilters.Tenant]);
        return await organizations
            .OrderBy(o => o.Name).ThenBy(o => o.Id)
            .Select(o => new PlatformOrganizationResponse(o.Id, o.Name, o.Slug, o.DefaultTimeZone, o.IsActive, o.CreatedAt,
                memberships.Count(m => m.OrganizationId == o.Id)))
            .ToPagedResultAsync(page, ct);
    }

    /// <summary>Creates the organization and invites its Owner by email.</summary>
    public async Task<Result<PlatformOrganizationResponse>> Create(CreatePlatformOrganizationCommand command, CancellationToken ct)
    {
        var normalized = User.Normalize(command.OwnerEmail);
        var owner = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized, ct);
        if (owner is null)
        {
            owner = new User
            {
                Email = command.OwnerEmail.Trim(),
                NormalizedEmail = normalized,
                FirstName = command.OwnerFirstName.Trim(),
                LastName = command.OwnerLastName.Trim(),
            };
            db.Users.Add(owner);
        }

        var (organization, membership) = await provisioner.ProvisionAsync(command.Name, owner, command.TimeZone, ct);
        membership.Status = MembershipStatus.Invited;
        membership.JoinedAt = null;

        var token = await invitations.CreateAsync(owner, membership, ct);
        await db.SaveChangesAsync(ct);
        await invitations.SendAsync(owner, token, ct);

        return new PlatformOrganizationResponse(organization.Id, organization.Name, organization.Slug,
            organization.DefaultTimeZone, organization.IsActive, organization.CreatedAt, 1);
    }

    /// <summary>Deactivated organizations cannot be signed into; existing refresh tokens stop working.</summary>
    public async Task<Result> SetActive(Guid organizationId, bool active, CancellationToken ct)
    {
        var organization = await db.Organizations.SingleOrDefaultAsync(o => o.Id == organizationId, ct);
        if (organization is null) return Error.NotFound("organization.not_found", "Organization not found.");

        organization.IsActive = active;
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
