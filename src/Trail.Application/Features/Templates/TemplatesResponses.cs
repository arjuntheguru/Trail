using Trail.Application.Features.Tags;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

public sealed record TemplateSummaryResponse(
    Guid Id,
    string Name,
    TaskKind Kind,
    IReadOnlyList<TagResponse> Tags,
    int FieldCount,
    int Version,
    bool IsActive,
    bool RequiresApproval,
    bool RequiresPhoto,
    string? LibraryKey);

public sealed record TemplateResponse(
    Guid Id,
    string Name,
    string? Description,
    TaskKind Kind,
    IReadOnlyList<TemplateField> Fields,
    bool RequiresApproval,
    bool RequiresPhoto,
    int Version,
    bool IsActive,
    string? LibraryKey,
    IReadOnlyList<Guid> TagIds);

public sealed record LibraryTemplateSummaryResponse(
    string Key, string Name, string Category, string Description, TaskKind Kind, int FieldCount);

public sealed record LibraryTemplateResponse(
    string Key,
    string Name,
    string Category,
    string Description,
    TaskKind Kind,
    bool RequiresApproval,
    bool RequiresPhoto,
    IReadOnlyList<TemplateField> Fields);

internal static class TemplateMapping
{
    public static TemplateResponse ToResponse(TaskTemplate t) => new(
        t.Id, t.Name, t.Description, t.Kind, t.Fields, t.RequiresApproval, t.RequiresPhoto, t.Version, t.IsActive,
        t.LibraryKey, t.Tags.Select(x => x.TagId).ToList());
}
