using System.Globalization;
using System.Text.Json;

namespace Trail.Domain.Tasks;

/// <summary>The response to one field. <see cref="Value"/> is raw JSON whose shape depends on the field type.</summary>
public sealed record FieldAnswer
{
    public JsonElement? Value { get; init; }
    public IReadOnlyList<Guid> PhotoIds { get; init; } = [];
    public string? Comment { get; init; }

    public bool HasValue => Value is { ValueKind: not (JsonValueKind.Null or JsonValueKind.Undefined) };
}

public sealed record FieldFailure(string FieldKey, string Label, string Reason);

public sealed record AnswerProblem(string FieldKey, string Message);

public sealed record EntryEvaluation(IReadOnlyList<AnswerProblem> Problems, IReadOnlyList<FieldFailure> Failures)
{
    public bool IsValid => Problems.Count == 0;
    public bool HasFailures => Failures.Count > 0;
}

/// <summary>
/// Checks answers against a template's fields: shape and type of each value, required fields, and whether an
/// answer is a failure (out-of-range reading, wrong yes/no, failing option) that needs evidence and follow-up.
/// </summary>
public static class AnswerEvaluator
{
    public const int TextMaxLength = 500;
    public const int LongTextMaxLength = 5000;
    public const int CommentMaxLength = 2000;
    public const int MaxPhotosPerAnswer = 10;

    /// <param name="requireAll">True on completion/submission: every required field must be answered.</param>
    public static EntryEvaluation Evaluate(
        IReadOnlyList<TemplateField> fields, IReadOnlyDictionary<string, FieldAnswer> answers, bool requireAll)
    {
        var problems = new List<AnswerProblem>();
        var failures = new List<FieldFailure>();
        var byKey = fields.ToDictionary(f => f.Key, StringComparer.Ordinal);

        foreach (var key in answers.Keys.Where(k => !byKey.TryGetValue(k, out var f) || !f.IsAnswerable))
            problems.Add(new(key, "This field does not exist on the task."));

        foreach (var field in fields.Where(f => f.IsAnswerable))
        {
            answers.TryGetValue(field.Key, out var answer);
            var answered = answer is not null && IsAnswered(field, answer);

            if (!answered)
            {
                if (requireAll && field.Required)
                    problems.Add(new(field.Key, $"'{field.Label}' is required."));
                continue;
            }

            if (answer!.Comment is { Length: > CommentMaxLength })
                problems.Add(new(field.Key, $"Comments are limited to {CommentMaxLength} characters."));
            if (answer.PhotoIds.Count > MaxPhotosPerAnswer)
                problems.Add(new(field.Key, $"Attach at most {MaxPhotosPerAnswer} photos."));

            var (error, failureReason) = Check(field, answer);
            if (error is not null)
            {
                problems.Add(new(field.Key, error));
                continue;
            }

            if (failureReason is null) continue;
            failures.Add(new(field.Key, field.Label, failureReason));

            if (!requireAll) continue;
            if (field.PhotoRequiredOnFailure && answer.PhotoIds.Count == 0)
                problems.Add(new(field.Key, $"'{field.Label}' failed: add a photo as evidence."));
            if (field.CommentRequiredOnFailure && string.IsNullOrWhiteSpace(answer.Comment))
                problems.Add(new(field.Key, $"'{field.Label}' failed: describe the corrective action taken."));
        }

        return new EntryEvaluation(problems, failures);
    }

    private static bool IsAnswered(TemplateField field, FieldAnswer answer) => field.Type switch
    {
        FieldType.Photo => answer.PhotoIds.Count > 0,
        FieldType.Checkbox => answer.Value is { ValueKind: JsonValueKind.True },
        FieldType.MultiSelect => answer.Value is { ValueKind: JsonValueKind.Array } array && array.GetArrayLength() > 0,
        FieldType.Text or FieldType.LongText => answer.Value is { ValueKind: JsonValueKind.String } s && !string.IsNullOrWhiteSpace(s.GetString()),
        _ => answer.HasValue,
    };

    /// <returns>An error when the value has the wrong shape, else the failure reason when the answer fails.</returns>
    private static (string? Error, string? Failure) Check(TemplateField field, FieldAnswer answer)
    {
        var value = answer.Value ?? default;
        switch (field.Type)
        {
            case FieldType.Checkbox:
                return (null, null);

            case FieldType.YesNo:
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return ("Answer yes or no.", null);
                var yes = value.GetBoolean();
                return (null, field.ExpectedAnswer is { } expected && expected != yes
                    ? $"Answered {(yes ? "yes" : "no")}, expected {(expected ? "yes" : "no")}."
                    : null);

            case FieldType.Number or FieldType.Temperature:
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var number)) return ("Enter a number.", null);
                return (null, OutOfRange(field, number));

            case FieldType.Text or FieldType.LongText:
                if (value.ValueKind != JsonValueKind.String) return ("Enter text.", null);
                var limit = field.Type == FieldType.Text ? TextMaxLength : LongTextMaxLength;
                return value.GetString()!.Length > limit ? ($"Text is limited to {limit} characters.", null) : (null, null);

            case FieldType.SingleSelect:
                if (value.ValueKind != JsonValueKind.String) return ("Choose an option.", null);
                var chosen = field.Options.FirstOrDefault(o => o.Value == value.GetString());
                if (chosen is null) return ("Choose one of the listed options.", null);
                return (null, chosen.IsFailure ? $"'{chosen.Label}' was selected." : null);

            case FieldType.MultiSelect:
                if (value.ValueKind != JsonValueKind.Array) return ("Choose one or more options.", null);
                var picked = new List<FieldOption>();
                foreach (var item in value.EnumerateArray())
                {
                    var option = item.ValueKind == JsonValueKind.String ? field.Options.FirstOrDefault(o => o.Value == item.GetString()) : null;
                    if (option is null) return ("Choose only the listed options.", null);
                    picked.Add(option);
                }
                var failing = picked.Where(o => o.IsFailure).Select(o => $"'{o.Label}'").ToList();
                return (null, failing.Count > 0 ? $"{string.Join(", ", failing)} selected." : null);

            case FieldType.Date:
                return value.ValueKind == JsonValueKind.String
                       && DateOnly.TryParseExact(value.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? (null, null)
                    : ("Enter a date as yyyy-MM-dd.", null);

            case FieldType.Time:
                return value.ValueKind == JsonValueKind.String
                       && TimeOnly.TryParseExact(value.GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)
                    ? (null, null)
                    : ("Enter a time as HH:mm.", null);

            case FieldType.Signature:
                return value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out _)
                    ? (null, null)
                    : ("Attach a signature.", null);

            case FieldType.Photo:
                return (null, null);

            default:
                return ("This field cannot be answered.", null);
        }
    }

    private static string? OutOfRange(TemplateField field, decimal value)
    {
        var unit = field.Unit ?? (field.Type == FieldType.Temperature ? "°C" : "");
        var shown = $"{value.ToString(CultureInfo.InvariantCulture)}{unit}";
        if (field.Min is { } min && value < min) return $"{shown} is below the minimum of {min.ToString(CultureInfo.InvariantCulture)}{unit}.";
        if (field.Max is { } max && value > max) return $"{shown} is above the maximum of {max.ToString(CultureInfo.InvariantCulture)}{unit}.";
        return null;
    }
}
