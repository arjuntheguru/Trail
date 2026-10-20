namespace Trail.Domain.Tasks;

public enum FieldType
{
    /// <summary>Tick when done. Value: boolean.</summary>
    Checkbox,

    /// <summary>Value: boolean. Fails when it differs from <see cref="TemplateField.ExpectedAnswer"/>.</summary>
    YesNo,

    /// <summary>Value: number. Fails outside <see cref="TemplateField.Min"/>..<see cref="TemplateField.Max"/>.</summary>
    Number,

    /// <summary>Value: number in <see cref="TemplateField.Unit"/> (°C by default). Same range rule as Number.</summary>
    Temperature,

    /// <summary>Value: string, up to 500 characters.</summary>
    Text,

    /// <summary>Value: string, up to 5000 characters.</summary>
    LongText,

    /// <summary>Value: one option value. Fails when the option is marked as a failure.</summary>
    SingleSelect,

    /// <summary>Value: array of option values. Fails when any chosen option is a failure.</summary>
    MultiSelect,

    /// <summary>Value: "yyyy-MM-dd" (e.g. a use-by date).</summary>
    Date,

    /// <summary>Value: "HH:mm".</summary>
    Time,

    /// <summary>Value: none; photos go in the answer's PhotoIds.</summary>
    Photo,

    /// <summary>Value: id of an uploaded signature image.</summary>
    Signature,

    /// <summary>Read-only guidance shown to the person completing the task. Never answered.</summary>
    Instruction,
}

public sealed record FieldOption(string Value, string Label, bool IsFailure = false);

/// <summary>
/// One question or check in a template. <see cref="Key"/> is stable across edits so answers and reports can be
/// matched to the field even after the template changes.
/// </summary>
public sealed record TemplateField
{
    public const int KeyMaxLength = 40;
    public const int LabelMaxLength = 300;

    public required string Key { get; init; }
    public required string Label { get; init; }
    public required FieldType Type { get; init; }
    public bool Required { get; init; } = true;
    public string? HelpText { get; init; }

    /// <summary>Optional heading the field is grouped under, e.g. "Walk-in fridge".</summary>
    public string? Section { get; init; }

    public decimal? Min { get; init; }
    public decimal? Max { get; init; }
    public string? Unit { get; init; }

    /// <summary>For YesNo: the passing answer. Null means neither answer is a failure.</summary>
    public bool? ExpectedAnswer { get; init; }

    public IReadOnlyList<FieldOption> Options { get; init; } = [];

    /// <summary>A failing answer must come with at least one photo.</summary>
    public bool PhotoRequiredOnFailure { get; init; }

    /// <summary>A failing answer must come with a comment explaining the corrective action.</summary>
    public bool CommentRequiredOnFailure { get; init; } = true;

    /// <summary>A failing answer automatically raises an issue for managers to follow up.</summary>
    public bool RaiseIssueOnFailure { get; init; } = true;

    public bool IsAnswerable => Type != FieldType.Instruction;
}
