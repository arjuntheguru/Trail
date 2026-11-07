using System.Text.Json;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Tasks;

public sealed class AnswerEvaluatorTests
{
    private static readonly TemplateField Fridge = new()
    {
        Key = "fridge", Label = "Walk-in fridge", Type = FieldType.Temperature, Min = 0, Max = 5, PhotoRequiredOnFailure = true,
    };
    private static readonly TemplateField Clean = new() { Key = "clean", Label = "Surfaces sanitised", Type = FieldType.YesNo, ExpectedAnswer = true };
    private static readonly TemplateField Tick = new() { Key = "tick", Label = "Lights on", Type = FieldType.Checkbox };
    private static readonly TemplateField Notes = new() { Key = "notes", Label = "Notes", Type = FieldType.LongText, Required = false };
    private static readonly TemplateField Info = new() { Key = "info", Label = "Read the HACCP plan", Type = FieldType.Instruction };
    private static readonly TemplateField Condition = new()
    {
        Key = "cond", Label = "Packaging", Type = FieldType.SingleSelect,
        Options = [new("ok", "Intact"), new("damaged", "Damaged", IsFailure: true)],
    };

    private static readonly TemplateField[] Fields = [Fridge, Clean, Tick, Notes, Info, Condition];

    private static FieldAnswer A(object? value, string? comment = null, params Guid[] photos) => new()
    {
        Value = value is null ? null : JsonSerializer.SerializeToElement(value),
        Comment = comment,
        PhotoIds = photos,
    };

    private static Dictionary<string, FieldAnswer> Passing() => new()
    {
        ["fridge"] = A(3.5m),
        ["clean"] = A(true),
        ["tick"] = A(true),
        ["cond"] = A("ok"),
    };

    [Fact]
    public void A_complete_passing_entry_is_valid_with_no_failures()
    {
        var result = AnswerEvaluator.Evaluate(Fields, Passing(), requireAll: true);

        result.IsValid.ShouldBeTrue(string.Join("; ", result.Problems.Select(p => p.Message)));
        result.HasFailures.ShouldBeFalse();
    }

    [Fact]
    public void Missing_required_answers_only_matter_on_completion()
    {
        var partial = new Dictionary<string, FieldAnswer> { ["fridge"] = A(2) };

        AnswerEvaluator.Evaluate(Fields, partial, requireAll: false).IsValid.ShouldBeTrue();
        AnswerEvaluator.Evaluate(Fields, partial, requireAll: true).Problems.Select(p => p.FieldKey)
            .ShouldBe(["clean", "tick", "cond"], ignoreOrder: true);
    }

    [Fact]
    public void An_unticked_checkbox_does_not_count_as_answered()
    {
        var answers = Passing();
        answers["tick"] = A(false);

        AnswerEvaluator.Evaluate(Fields, answers, requireAll: true).Problems.ShouldHaveSingleItem().FieldKey.ShouldBe("tick");
    }

    [Fact]
    public void Out_of_range_temperature_fails_and_needs_photo_and_corrective_action()
    {
        var answers = Passing();
        answers["fridge"] = A(8.2m);

        var withoutEvidence = AnswerEvaluator.Evaluate(Fields, answers, requireAll: true);
        withoutEvidence.Failures.ShouldHaveSingleItem().Reason.ShouldBe("8.2°C is above the maximum of 5°C.");
        withoutEvidence.Problems.Count.ShouldBe(2);

        answers["fridge"] = A(8.2m, "Moved stock to backup fridge, called engineer", Guid.NewGuid());
        var withEvidence = AnswerEvaluator.Evaluate(Fields, answers, requireAll: true);
        withEvidence.IsValid.ShouldBeTrue();
        withEvidence.HasFailures.ShouldBeTrue();
    }

    [Fact]
    public void Wrong_yes_no_and_failing_options_are_failures()
    {
        var answers = Passing();
        answers["clean"] = A(false, "Re-cleaned");
        answers["cond"] = A("damaged", "Rejected delivery");

        var result = AnswerEvaluator.Evaluate(Fields, answers, requireAll: true);

        result.IsValid.ShouldBeTrue();
        result.Failures.Select(f => f.FieldKey).ShouldBe(["clean", "cond"], ignoreOrder: true);
    }

    [Theory]
    [InlineData("fridge", "\"warm\"", "Enter a number.")]
    [InlineData("clean", "\"maybe\"", "Answer yes or no.")]
    [InlineData("cond", "\"unknown\"", "Choose one of the listed options.")]
    public void Wrongly_shaped_values_are_rejected(string key, string json, string message)
    {
        var answers = Passing();
        answers[key] = new FieldAnswer { Value = JsonDocument.Parse(json).RootElement.Clone() };

        AnswerEvaluator.Evaluate(Fields, answers, requireAll: false).Problems.ShouldHaveSingleItem().Message.ShouldBe(message);
    }

    [Fact]
    public void Answers_to_unknown_or_instruction_fields_are_rejected()
    {
        var answers = Passing();
        answers["info"] = A("read it");
        answers["ghost"] = A(1);

        AnswerEvaluator.Evaluate(Fields, answers, requireAll: false).Problems.Select(p => p.FieldKey).ShouldBe(["info", "ghost"], ignoreOrder: true);
    }

    [Fact]
    public void Template_definitions_are_validated()
    {
        TemplateField[] bad =
        [
            new() { Key = "a", Label = "Range", Type = FieldType.Number, Min = 10, Max = 1 },
            new() { Key = "a", Label = "Dup", Type = FieldType.Text },
            new() { Key = "bad key!", Label = "Key", Type = FieldType.Checkbox },
            new() { Key = "sel", Label = "Select", Type = FieldType.SingleSelect, Options = [new("x", "X")] },
            new() { Key = "yn", Label = "Text expected", Type = FieldType.Text, ExpectedAnswer = true },
        ];

        var errors = TemplateDefinition.Validate(bad);

        errors.ShouldContain(e => e.Contains("minimum cannot be greater"));
        errors.ShouldContain(e => e.Contains("used more than once"));
        errors.ShouldContain(e => e.Contains("key must be"));
        errors.ShouldContain(e => e.Contains("between 2 and 50 options"));
        errors.ShouldContain(e => e.Contains("only yes/no fields"));
        TemplateDefinition.Validate(Fields).ShouldBeEmpty();
        TemplateDefinition.Validate([Info]).ShouldContain(e => e.Contains("can be answered"));
    }
}
