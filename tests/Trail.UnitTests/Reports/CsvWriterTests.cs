using System.Text;
using Trail.Application.Features.Reports;

namespace Trail.UnitTests.Reports;

public sealed class CsvWriterTests
{
    private static string Row(params object?[] values) => CsvWriter.FormatRow(values);

    [Fact]
    public void Plain_fields_are_joined_with_commas_and_end_with_crlf() =>
        Row("a", "b", "c").ShouldBe("a,b,c\r\n");

    [Theory]
    [InlineData("a,b", "\"a,b\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("line1\nline2", "\"line1\nline2\"")]
    [InlineData("line1\r\nline2", "\"line1\r\nline2\"")]
    [InlineData("a\rb", "\"a\rb\"")]
    [InlineData("plain", "plain")]
    public void Fields_are_quoted_only_when_needed(string input, string expected) =>
        CsvWriter.FormatField(input).ShouldBe(expected);

    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+cmd", "'+cmd")]
    [InlineData("-5 apples", "'-5 apples")]
    [InlineData("@SUM(A1)", "'@SUM(A1)")]
    [InlineData("\tindented", "'\tindented")]
    public void Text_starting_with_a_formula_character_is_prefixed(string input, string expected) =>
        CsvWriter.FormatField(input).ShouldBe(expected);

    [Fact]
    public void Prefixed_text_that_also_needs_quoting_is_quoted()
    {
        CsvWriter.FormatField("=A1,B1").ShouldBe("\"'=A1,B1\"");
        CsvWriter.FormatField("\rcarriage").ShouldBe("\"'\rcarriage\"");
    }

    [Fact]
    public void Formula_characters_inside_text_are_left_alone() =>
        CsvWriter.FormatField("a=b").ShouldBe("a=b");

    [Fact]
    public void Numbers_are_written_verbatim_even_when_negative()
    {
        CsvWriter.FormatField(-5).ShouldBe("-5");
        CsvWriter.FormatField(-2.5m).ShouldBe("-2.5");
        CsvWriter.FormatField(-1.5d).ShouldBe("-1.5");
        CsvWriter.FormatField(42L).ShouldBe("42");
    }

    [Fact]
    public void Numbers_use_the_invariant_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
            CsvWriter.FormatField(1234.5m).ShouldBe("1234.5");
            CsvWriter.FormatField(0.25d).ShouldBe("0.25");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Nulls_are_empty_fields() =>
        Row("a", null, "c", null).ShouldBe("a,,c,\r\n");

    [Fact]
    public void Booleans_and_ids_are_written_as_values()
    {
        CsvWriter.FormatField(true).ShouldBe("true");
        CsvWriter.FormatField(false).ShouldBe("false");
        var id = Guid.Parse("0190a1b2-0000-7000-8000-000000000001");
        CsvWriter.FormatField(id).ShouldBe("0190a1b2-0000-7000-8000-000000000001");
    }

    [Fact]
    public void Dates_are_iso_8601_utc()
    {
        CsvWriter.FormatField(new DateTime(2026, 10, 1, 8, 30, 15, DateTimeKind.Utc)).ShouldBe("2026-10-01T08:30:15Z");
        CsvWriter.FormatField(new DateTime(2026, 10, 1, 8, 30, 15, 250, DateTimeKind.Utc)).ShouldBe("2026-10-01T08:30:15.25Z");
        CsvWriter.FormatField(new DateTimeOffset(2026, 10, 1, 10, 0, 0, TimeSpan.FromHours(2))).ShouldBe("2026-10-01T08:00:00Z");
        CsvWriter.FormatField(new DateOnly(2026, 10, 1)).ShouldBe("2026-10-01");
    }

    public enum Sample
    {
        Alpha,
    }

    [Fact]
    public void Enums_are_written_by_name() => CsvWriter.FormatField(Sample.Alpha).ShouldBe("Alpha");

    [Fact]
    public async Task The_stream_starts_with_a_utf8_bom_and_keeps_non_ascii_text()
    {
        using var stream = new MemoryStream();
        await using (var csv = new CsvWriter(stream))
        {
            await csv.WriteRowAsync("Name", "City");
            await csv.WriteRowAsync("Zoë", "Köln");
        }

        var bytes = stream.ToArray();
        bytes.Take(3).ShouldBe(new byte[] { 0xEF, 0xBB, 0xBF });
        Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).ShouldBe("Name,City\r\nZoë,Köln\r\n");
    }
}
