using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Issues;

public sealed class GetIssueHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<IssueDetailResponse>> Handle(Guid id, CancellationToken ct)
    {
        // Issues at sites outside the caller's scope look exactly like issues that do not exist.
        var issue = await IssueRules.FindAsync(db, access, id, ct);
        return issue is null
            ? IssueRules.NotFound
            : await IssueRules.LoadDetailAsync(db, id, clock.GetUtcNow().UtcDateTime, ct);
    }
}
