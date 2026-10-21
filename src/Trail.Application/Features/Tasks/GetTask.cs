using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Tasks;

public sealed class GetTaskHandler(IAppDbContext db, ISiteAccess access, TimeProvider clock)
{
    public async Task<Result<TaskDetailResponse>> Handle(Guid id, CancellationToken ct)
    {
        var scope = await access.GetScopeAsync(ct);
        var task = await db.SiteTasks.AsNoTracking().Include(t => t.Entries).FirstOrDefaultAsync(t => t.Id == id, ct);
        return task is null || !scope.Includes(task.SiteId)
            ? TaskRules.NotFound
            : await TaskMapping.ToDetailAsync(db, task, clock.GetUtcNow().UtcDateTime, ct);
    }
}
