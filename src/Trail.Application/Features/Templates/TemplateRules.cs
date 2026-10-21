using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Trail.Application.Common.Abstractions;
using Trail.Application.Common.Results;
using Trail.Domain.Tasks;

namespace Trail.Application.Features.Templates;

/// <summary>The fields shared by creating and updating a template.</summary>
public interface ITemplateInput
{
    string Name { get; }
    string? Description { get; }
    TaskKind Kind { get; }
    IReadOnlyList<TemplateField> Fields { get; }
    bool RequiresApproval { get; }
    bool RequiresPhoto { get; }
    IReadOnlyList<Guid>? TagIds { get; }
}

internal abstract class TemplateInputValidator<T> : AbstractValidator<T> where T : ITemplateInput
{
    protected TemplateInputValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(TaskTemplate.NameMaxLength);
        RuleFor(x => x.Description).MaximumLength(TaskTemplate.DescriptionMaxLength);
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Fields).NotNull();
        RuleForEach(x => x.Fields).NotNull();
    }
}

internal static class TemplateRules
{
    /// <summary>Checks the field list, tags and name. <paramref name="excludeId"/> is the template being updated.</summary>
    public static async Task<(Error? Error, List<Guid> TagIds, string Name)> CheckAsync(
        IAppDbContext db, ITemplateInput input, Guid? excludeId, CancellationToken ct)
    {
        var problems = TemplateDefinition.Validate(input.Fields);
        if (problems.Count > 0)
        {
            return (Error.Validation("template.invalid_fields", "The template's fields are not valid.",
                new Dictionary<string, string[]> { ["fields"] = [.. problems] }), [], "");
        }

        var tagIds = (input.TagIds ?? []).Distinct().ToList();
        if (tagIds.Count > 0 && await db.Tags.CountAsync(t => tagIds.Contains(t.Id), ct) != tagIds.Count)
            return (Error.Validation("template.unknown_tags", "One or more tags do not exist."), [], "");

        var name = input.Name.Trim();
        if (await db.TaskTemplates.AnyAsync(t => t.Name == name && t.Id != excludeId, ct))
            return (Error.Conflict("template.name_taken", "A template with this name already exists."), [], "");

        return (null, tagIds, name);
    }

    public static bool FieldsEqual(IReadOnlyList<TemplateField> a, IReadOnlyList<TemplateField> b) =>
        JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);
}
