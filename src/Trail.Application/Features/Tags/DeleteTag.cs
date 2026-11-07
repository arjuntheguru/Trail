using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;

namespace Trail.Application.Features.Tags;

public sealed class DeleteTagHandler(IAppDbContext db)
{
    public async Task<Result> Handle(Guid id, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (tag is null) return Error.NotFound("tag.not_found", "Tag not found.");

        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
