namespace Trail.Domain.Tasks;

/// <summary>Validates the shape of a template's field list before it is saved.</summary>
public static class TemplateDefinition
{
    public const int MaxFields = 200;

    private static readonly HashSet<FieldType> RangeTypes = [FieldType.Number, FieldType.Temperature];
    private static readonly HashSet<FieldType> OptionTypes = [FieldType.SingleSelect, FieldType.MultiSelect];

    public static IReadOnlyList<string> Validate(IReadOnlyList<TemplateField> fields)
    {
        var errors = new List<string>();
        if (fields.Count == 0) errors.Add("A template needs at least one field.");
        if (fields.Count > MaxFields) errors.Add($"A template can have at most {MaxFields} fields.");
        if (!fields.Any(f => f.IsAnswerable)) errors.Add("A template needs at least one field that can be answered.");

        foreach (var duplicate in fields.GroupBy(f => f.Key, StringComparer.Ordinal).Where(g => g.Count() > 1))
            errors.Add($"Field key '{duplicate.Key}' is used more than once.");

        foreach (var field in fields)
        {
            var name = string.IsNullOrWhiteSpace(field.Label) ? field.Key : field.Label;
            if (string.IsNullOrWhiteSpace(field.Key) || field.Key.Length > TemplateField.KeyMaxLength
                || !field.Key.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'))
                errors.Add($"Field '{name}': key must be 1-{TemplateField.KeyMaxLength} letters, digits, '_' or '-'.");
            if (string.IsNullOrWhiteSpace(field.Label) || field.Label.Length > TemplateField.LabelMaxLength)
                errors.Add($"Field '{field.Key}': label is required (max {TemplateField.LabelMaxLength} characters).");

            if (RangeTypes.Contains(field.Type))
            {
                if (field.Min is { } min && field.Max is { } max && min > max)
                    errors.Add($"Field '{name}': minimum cannot be greater than maximum.");
            }
            else if (field.Min is not null || field.Max is not null)
            {
                errors.Add($"Field '{name}': only number and temperature fields have a range.");
            }

            if (OptionTypes.Contains(field.Type))
            {
                if (field.Options.Count is < 2 or > 50)
                    errors.Add($"Field '{name}': give between 2 and 50 options.");
                if (field.Options.Any(o => string.IsNullOrWhiteSpace(o.Value) || string.IsNullOrWhiteSpace(o.Label)))
                    errors.Add($"Field '{name}': every option needs a value and a label.");
                if (field.Options.Select(o => o.Value).Distinct(StringComparer.Ordinal).Count() != field.Options.Count)
                    errors.Add($"Field '{name}': option values must be unique.");
            }
            else if (field.Options.Count > 0)
            {
                errors.Add($"Field '{name}': only select fields have options.");
            }

            if (field.ExpectedAnswer is not null && field.Type != FieldType.YesNo)
                errors.Add($"Field '{name}': only yes/no fields have an expected answer.");
        }

        return errors;
    }
}
