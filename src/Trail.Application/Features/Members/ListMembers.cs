using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Common.Results;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Members;

public sealed record ListMembersQuery(string? Search = null, MembershipStatus? Status = null, Guid? RoleId = null);

public sealed class ListMembersHandler(IAppDbContext db)
{
    public async Task<PagedResult<MemberResponse>> Handle(ListMembersQuery query, PageRequest page, CancellationToken ct)
    {
        var members = db.Memberships.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            members = members.Where(m =>
                m.User.FirstName.Contains(term) || m.User.LastName.Contains(term) || m.User.Email.Contains(term));
        }
        if (query.Status is { } status) members = members.Where(m => m.Status == status);
        if (query.RoleId is { } roleId) members = members.Where(m => m.RoleId == roleId);

        return await members
            .OrderBy(m => m.User.FirstName).ThenBy(m => m.User.LastName).ThenBy(m => m.Id)
            .Select(MemberResponse.Projection)
            .ToPagedResultAsync(page, ct);
    }
}

public sealed class GetMemberHandler(IAppDbContext db)
{
    public async Task<Result<MemberResponse>> Handle(Guid membershipId, CancellationToken ct)
    {
        var member = await db.Memberships.AsNoTracking()
            .Where(m => m.Id == membershipId)
            .Select(MemberResponse.Projection)
            .SingleOrDefaultAsync(ct);
        return member is null ? MemberRules.NotFound : member;
    }
}
