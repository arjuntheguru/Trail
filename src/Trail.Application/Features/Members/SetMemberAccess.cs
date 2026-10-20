using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Members;

/// <summary>Replaces a member's access: the lists are the complete new assignment, not additions.</summary>
public sealed record SetMemberAccessCommand(
    bool AllSites,
    IReadOnlyList<Guid>? SiteIds,
    IReadOnlyList<Guid>? RegionIds,
    IReadOnlyList<Guid>? DepartmentIds);

internal sealed class SetMemberAccessValidator : AbstractValidator<SetMemberAccessCommand>
{
    private const int MaxAssignments = 1000;

    public SetMemberAccessValidator()
    {
        RuleFor(x => x.SiteIds).Must(ids => ids!.Count <= MaxAssignments).When(x => x.SiteIds is not null);
        RuleFor(x => x.RegionIds).Must(ids => ids!.Count <= MaxAssignments).When(x => x.RegionIds is not null);
        RuleFor(x => x.DepartmentIds).Must(ids => ids!.Count <= MaxAssignments).When(x => x.DepartmentIds is not null);
        RuleForEach(x => x.SiteIds).NotEmpty();
        RuleForEach(x => x.RegionIds).NotEmpty();
        RuleForEach(x => x.DepartmentIds).NotEmpty();
    }
}

public sealed class SetMemberAccessHandler(IAppDbContext db, ICurrentUser user, ISiteAccess access)
{
    public async Task<Result<MemberAccessResponse>> Handle(
        Guid membershipId, SetMemberAccessCommand command, CancellationToken ct)
    {
        var membership = await db.Memberships.FirstOrDefaultAsync(m => m.Id == membershipId, ct);
        if (membership is null) return Error.NotFound("member.not_found", "Member not found.");

        if (membership.Id == user.MembershipId)
            return Error.Forbidden("member.cannot_change_own_access", "You cannot change your own access.");

        var siteIds = (command.SiteIds ?? []).Distinct().ToList();
        var regionIds = (command.RegionIds ?? []).Distinct().ToList();
        var departmentIds = (command.DepartmentIds ?? []).Distinct().ToList();

        var scope = await access.GetScopeAsync(ct);
        if (!scope.AllSites)
        {
            // Nobody can hand out more than they have, and nobody can edit access that is wider than theirs
            // (replacing it would also strip the part they cannot see).
            var outOfScope = command.AllSites
                || membership.AllSites
                || siteIds.Any(id => !scope.SiteIds.Contains(id))
                || regionIds.Any(id => !scope.RegionIds.Contains(id))
                || (await GetMemberAccessHandler.LiveSiteIdsAsync(db, membershipId, ct)).Any(id => !scope.SiteIds.Contains(id))
                || (await GetMemberAccessHandler.LiveRegionIdsAsync(db, membershipId, ct)).Any(id => !scope.RegionIds.Contains(id));
            if (outOfScope)
                return Error.Forbidden("member.access_out_of_scope", "You can only manage access within your own sites and regions.");
        }

        if (await db.Sites.CountAsync(s => siteIds.Contains(s.Id), ct) != siteIds.Count
            || await db.Regions.CountAsync(r => regionIds.Contains(r.Id), ct) != regionIds.Count
            || await db.Departments.CountAsync(d => departmentIds.Contains(d.Id), ct) != departmentIds.Count)
            return Error.Validation("member.unknown_ids", "One or more sites, regions or departments do not exist.");

        membership.AllSites = command.AllSites;

        var currentSites = await db.MembershipSites.Where(x => x.MembershipId == membershipId).ToListAsync(ct);
        db.MembershipSites.RemoveRange(currentSites.Where(x => !siteIds.Contains(x.SiteId)));
        db.MembershipSites.AddRange(siteIds
            .Where(id => currentSites.All(x => x.SiteId != id))
            .Select(id => new MembershipSite { MembershipId = membershipId, SiteId = id }));

        var currentRegions = await db.MembershipRegions.Where(x => x.MembershipId == membershipId).ToListAsync(ct);
        db.MembershipRegions.RemoveRange(currentRegions.Where(x => !regionIds.Contains(x.RegionId)));
        db.MembershipRegions.AddRange(regionIds
            .Where(id => currentRegions.All(x => x.RegionId != id))
            .Select(id => new MembershipRegion { MembershipId = membershipId, RegionId = id }));

        var currentDepartments = await db.MembershipDepartments.Where(x => x.MembershipId == membershipId).ToListAsync(ct);
        db.MembershipDepartments.RemoveRange(currentDepartments.Where(x => !departmentIds.Contains(x.DepartmentId)));
        db.MembershipDepartments.AddRange(departmentIds
            .Where(id => currentDepartments.All(x => x.DepartmentId != id))
            .Select(id => new MembershipDepartment { MembershipId = membershipId, DepartmentId = id }));

        await db.SaveChangesAsync(ct);

        return new MemberAccessResponse(
            command.AllSites, siteIds.Order().ToList(), regionIds.Order().ToList(), departmentIds.Order().ToList());
    }
}
