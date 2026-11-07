using System.Linq.Expressions;
using Trail.Domain.Identity;

namespace Trail.Application.Features.Members;

public sealed record MemberResponse(
    Guid Id,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    Guid RoleId,
    string RoleName,
    MembershipStatus Status,
    bool AllSites,
    DateTime? JoinedAt,
    DateTime? LastLoginAt,
    DateTime CreatedAt)
{
    public static readonly Expression<Func<Membership, MemberResponse>> Projection = m => new MemberResponse(
        m.Id, m.UserId, m.User.Email, m.User.FirstName, m.User.LastName, m.RoleId, m.Role.Name,
        m.Status, m.AllSites, m.JoinedAt, m.User.LastLoginAt, m.CreatedAt);
}
