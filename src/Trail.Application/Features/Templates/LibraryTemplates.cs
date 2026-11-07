using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

public sealed class ListLibraryTemplatesHandler
{
    public IReadOnlyList<LibraryTemplateSummaryResponse> Handle() =>
        TemplateLibrary.All
            .Select(t => new LibraryTemplateSummaryResponse(t.Key, t.Name, t.Category, t.Description, t.Kind, t.Fields.Count))
            .ToList();
}

public sealed class GetLibraryTemplateHandler
{
    public Result<LibraryTemplateResponse> Handle(string key)
    {
        var t = TemplateLibrary.Find(key);
        return t is null
            ? LibraryErrors.NotFound
            : new LibraryTemplateResponse(t.Key, t.Name, t.Category, t.Description, t.Kind, t.RequiresApproval, t.RequiresPhoto, t.Fields);
    }
}

public sealed class CopyLibraryTemplateHandler(IAppDbContext db)
{
    public async Task<Result<TemplateResponse>> Handle(string key, CancellationToken ct)
    {
        var source = TemplateLibrary.Find(key);
        if (source is null) return LibraryErrors.NotFound;

        // Copying twice is allowed: later copies get " (2)", " (3)" and so on.
        var taken = (await db.TaskTemplates.AsNoTracking()
            .Where(t => t.Name.StartsWith(source.Name))
            .Select(t => t.Name)
            .ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var name = source.Name;
        for (var n = 2; taken.Contains(name); n++) name = $"{source.Name} ({n})";

        var template = new TaskTemplate
        {
            Name = name,
            Description = source.Description,
            Kind = source.Kind,
            Fields = [.. source.Fields],
            RequiresApproval = source.RequiresApproval,
            RequiresPhoto = source.RequiresPhoto,
            LibraryKey = source.Key,
        };
        db.TaskTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return TemplateMapping.ToResponse(template);
    }
}

internal static class LibraryErrors
{
    public static readonly Error NotFound = Error.NotFound("template.library_not_found", "Library template not found.");
}
