using Microsoft.EntityFrameworkCore;

using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Issues;

public sealed record ListAssigneeCandidatesQuery(Guid SiteId, string? Search = null);

public sealed class ListAssigneeCandidatesHandler(IAppDbContext db, ISiteAccess access)
{
    public const int MaxResults = 50;

    /// <summary>Members that <see cref="IssueRules.ValidateAssigneeAsync"/> would accept for the site, by name.</summary>
    public async Task<Result<IReadOnlyList<AssigneeCandidateResponse>>> Handle(ListAssigneeCandidatesQuery query, CancellationToken ct)
    {
        // A site the caller cannot reach looks like one that does not exist.
        if (!await access.CanAccessSiteAsync(query.SiteId, ct))
            return Error.NotFound("issue.unknown_site", "The site does not exist.");

        var regionId = await IssueRules.RegionOfAsync(db, query.SiteId, ct);
        var members = IssueRules.ReachingSite(db, query.SiteId, regionId);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            members = members.Where(m =>
                m.User.FirstName.Contains(term) || m.User.LastName.Contains(term) || m.User.Email.Contains(term));
        }

        // Permissions live in a json column, so the issues-view check happens after loading.
        var rows = await members
            .OrderBy(m => m.User.FirstName).ThenBy(m => m.User.LastName).ThenBy(m => m.Id)
            .Select(m => new Row(m.Id, m.UserId, m.User.FirstName, m.User.LastName, m.User.Email, m.Role.Name, m.Role.Permissions))
            .ToListAsync(ct);

        return rows.Where(r => IssueRules.CanViewIssues(r.Permissions)).Take(MaxResults)
            .Select(r => new AssigneeCandidateResponse(r.MembershipId, r.UserId, $"{r.FirstName} {r.LastName}".Trim(), r.Email, r.RoleName))
            .ToList();
    }

    private sealed record Row(
        Guid MembershipId, Guid UserId, string FirstName, string LastName, string Email, string RoleName, List<string> Permissions);
}