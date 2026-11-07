using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Paging;
using Trail.Application.Features.Tags;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

public sealed record ListTemplatesQuery(string? Search = null, TaskKind? Kind = null, Guid? TagId = null, bool IncludeInactive = false);

public sealed class ListTemplatesHandler(IAppDbContext db)
{
    public async Task<PagedResult<TemplateSummaryResponse>> Handle(ListTemplatesQuery query, PageRequest page, CancellationToken ct)
    {
        var templates = db.TaskTemplates.AsNoTracking();
        if (!query.IncludeInactive) templates = templates.Where(t => t.IsActive);
        if (query.Kind is { } kind) templates = templates.Where(t => t.Kind == kind);
        if (query.TagId is { } tagId) templates = templates.Where(t => t.Tags.Any(x => x.TagId == tagId));
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            templates = templates.Where(t => t.Name.Contains(term));
        }

        var paged = await templates
            .OrderBy(t => t.Name).ThenBy(t => t.Id)
            .Select(t => new Row(t.Id, t.Name, t.Kind, t.Fields, t.Version, t.IsActive, t.RequiresApproval, t.RequiresPhoto, t.LibraryKey))
            .ToPagedResultAsync(page, ct);

        var ids = paged.Items.Select(r => r.Id).ToList();
        var links = await db.TaskTemplateTags.AsNoTracking()
            .Where(x => ids.Contains(x.TemplateId))
            .Join(db.Tags, x => x.TagId, tag => tag.Id, (x, tag) => new { x.TemplateId, tag.Id, tag.Name, tag.Color })
            .ToListAsync(ct);
        var tagsByTemplate = links
            .GroupBy(l => l.TemplateId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<TagResponse>)g
                .OrderBy(l => l.Name).Select(l => new TagResponse(l.Id, l.Name, l.Color)).ToList());

        return new PagedResult<TemplateSummaryResponse>(
            paged.Items.Select(r => new TemplateSummaryResponse(
                r.Id, r.Name, r.Kind, tagsByTemplate.GetValueOrDefault(r.Id) ?? [], r.Fields.Count, r.Version,
                r.IsActive, r.RequiresApproval, r.RequiresPhoto, r.LibraryKey)).ToList(),
            paged.Page, paged.PageSize, paged.TotalCount);
    }

    private sealed record Row(
        Guid Id, string Name, TaskKind Kind, List<TemplateField> Fields, int Version, bool IsActive,
        bool RequiresApproval, bool RequiresPhoto, string? LibraryKey);
}
