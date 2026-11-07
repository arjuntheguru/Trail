using System.Linq.Expressions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Members;

/// <summary>
/// The rules for granting site and region access, shared by <c>PUT /members/{id}/access</c> and invitations
/// so both enforce exactly the same thing.
/// </summary>
internal static class MemberAccessRules
{
    public const int MaxAssignments = 1000;

    public static readonly Error OutOfScope =
        Error.Forbidden("member.access_out_of_scope", "You can only manage access within your own sites and regions.");

    public static readonly Error UnknownIds =
        Error.Validation("member.unknown_ids", "One or more sites, regions or departments do not exist.");

    /// <summary>Shape rules for a list of assigned ids: bounded size, no empty guids.</summary>
    public static void AddAccessIdRules<T>(AbstractValidator<T> validator, Expression<Func<T, IEnumerable<Guid>?>> ids)
    {
        validator.RuleFor(ids).Must(list => list is null || list.Count() <= MaxAssignments);
        validator.RuleForEach(ids!).NotEmpty();
    }

    /// <summary>
    /// True when nobody with <paramref name="scope"/> may hand out this access: nobody can give more than they
    /// have. Callers with all sites may grant anything.
    /// </summary>
    public static bool IsOutOfScope(
        SiteScope scope, bool allSites, IReadOnlyCollection<Guid> siteIds, IReadOnlyCollection<Guid> regionIds) =>
        !scope.AllSites
        && (allSites
            || siteIds.Any(id => !scope.SiteIds.Contains(id))
            || regionIds.Any(id => !scope.RegionIds.Contains(id)));

    /// <summary>True when every site and region id exists and is visible to the caller (tenant and soft-delete filters apply).</summary>
    public static async Task<bool> AllExistAsync(
        IAppDbContext db, IReadOnlyCollection<Guid> siteIds, IReadOnlyCollection<Guid> regionIds, CancellationToken ct) =>
        await db.Sites.CountAsync(s => siteIds.Contains(s.Id), ct) == siteIds.Count
        && await db.Regions.CountAsync(r => regionIds.Contains(r.Id), ct) == regionIds.Count;
}