using Trail.Application.Features.Integrations;
using Trail.Domain.Tasks;

namespace Trail.UnitTests.Integrations;

public sealed class SensorReadingRulesTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 12, 0, 0, DateTimeKind.Utc);

    private static readonly TemplateField Fridge = new()
    {
        Key = "fridge", Label = "Walk-in fridge", Type = FieldType.Temperature, Min = 0, Max = 5, PhotoRequiredOnFailure = true,
    };
    private static readonly TemplateField Checked = new() { Key = "checked", Label = "Door seal checked", Type = FieldType.YesNo };

    [Theory]
    [InlineData(0, null)]
    [InlineData(-3600, null)]
    [InlineData(-86400, null)]                              // exactly 24h old is still fine
    [InlineData(-86401, "rejected:too_old")]
    [InlineData(300, null)]                                 // exactly 5 minutes ahead is fine
    [InlineData(301, "rejected:in_future")]
    public void Reading_time_must_be_recent_and_not_in_the_future(int offsetSeconds, string? expected) =>
        SensorReadingRules.CheckTime(Now.AddSeconds(offsetSeconds), Now).ShouldBe(expected);

    [Fact]
    public void Times_are_cut_to_whole_seconds_in_utc()
    {
        var raw = new DateTime(2026, 3, 10, 12, 0, 5, DateTimeKind.Utc).AddTicks(1234567);

        var normalized = SensorReadingRules.Normalize(raw);

        normalized.ShouldBe(new DateTime(2026, 3, 10, 12, 0, 5, DateTimeKind.Utc));
        normalized.Kind.ShouldBe(DateTimeKind.Utc);
        SensorReadingRules.Normalize(normalized).ShouldBe(normalized);
    }

    [Theory]
    [InlineData("Europe/London", "2026-03-10T23:30:00Z", "2026-03-10")]
    [InlineData("Europe/London", "2026-06-10T23:30:00Z", "2026-06-11")]  // BST: already tomorrow
    [InlineData("America/New_York", "2026-03-10T03:30:00Z", "2026-03-09")]
    [InlineData("Asia/Tokyo", "2026-03-10T16:00:00Z", "2026-03-11")]
    [InlineData("Not/AZone", "2026-03-10T23:30:00Z", "2026-03-10")]      // unknown zone falls back to UTC
    public void Readings_belong_to_the_site_local_day(string zone, string utc, string expected) =>
        SensorReadingRules.LocalDate(DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal), zone)
            .ShouldBe(DateOnly.Parse(expected));

    [Fact]
    public void A_reading_becomes_a_single_temperature_answer()
    {
        var answers = SensorReadingRules.ToAnswers("fridge", 3.5m);

        answers.Keys.ShouldBe(["fridge"]);
        answers["fridge"].Value!.Value.GetDecimal().ShouldBe(3.5m);
        answers["fridge"].PhotoIds.ShouldBeEmpty();
        answers["fridge"].Comment.ShouldBeNull();
    }

    [Fact]
    public void In_range_reading_passes_the_evaluator_without_other_fields_or_evidence()
    {
        var evaluation = AnswerEvaluator.Evaluate([Fridge, Checked], SensorReadingRules.ToAnswers("fridge", 3.5m), requireAll: false);

        evaluation.IsValid.ShouldBeTrue();
        evaluation.HasFailures.ShouldBeFalse();
    }

    [Theory]
    [InlineData(7.2, "above the maximum")]
    [InlineData(-1.0, "below the minimum")]
    public void Out_of_range_reading_is_a_failure_even_though_a_sensor_cannot_add_a_photo_or_comment(double value, string reason)
    {
        var evaluation = AnswerEvaluator.Evaluate([Fridge], SensorReadingRules.ToAnswers("fridge", (decimal)value), requireAll: false);

        evaluation.IsValid.ShouldBeTrue();
        evaluation.Failures.Count.ShouldBe(1);
        evaluation.Failures[0].FieldKey.ShouldBe("fridge");
        evaluation.Failures[0].Reason.ShouldContain(reason);
    }

    [Fact]
    public void The_same_reading_is_rejected_for_a_person_without_evidence()
    {
        var evaluation = AnswerEvaluator.Evaluate([Fridge], SensorReadingRules.ToAnswers("fridge", 7.2m), requireAll: true);

        evaluation.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void A_field_missing_from_the_task_is_a_problem()
    {
        var evaluation = AnswerEvaluator.Evaluate([Checked], SensorReadingRules.ToAnswers("fridge", 3m), requireAll: false);

        evaluation.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Note_names_the_sensor_and_is_bounded()
    {
        SensorReadingRules.Note("Fridge 2").ShouldBe("Recorded by sensor Fridge 2");
        SensorReadingRules.Note(new string('x', 5000)).Length.ShouldBe(SiteTask.NoteMaxLength);
    }

    [Fact]
    public void Result_codes_are_stable()
    {
        SensorResults.Accepted.ShouldBe("accepted");
        SensorResults.NoOpenTask.ShouldBe("skipped:no_open_task");
        SensorResults.UnknownDevice.ShouldBe("rejected:unknown_device");
        SensorResults.TooOld.ShouldBe("rejected:too_old");
        SensorResults.InFuture.ShouldBe("rejected:in_future");
        SensorReadingRules.MaxReadingsPerRequest.ShouldBe(500);
    }
}
