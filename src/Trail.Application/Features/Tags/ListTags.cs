using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;

namespace Trail.Application.Features.Tags;

public sealed class ListTagsHandler(IAppDbContext db)
{
    public async Task<IReadOnlyList<TagResponse>> Handle(CancellationToken ct) =>
        await db.Tags.AsNoTracking()
            .OrderBy(t => t.Name)
            .Select(t => new TagResponse(t.Id, t.Name, t.Color))
            .ToListAsync(ct);
}
