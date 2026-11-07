using System.Globalization;
using System.Text;

namespace Trail.Application.Features.Reports;

/// <summary>
/// RFC 4180 CSV output. Strings are quoted when needed and neutralised against spreadsheet formula injection;
/// numbers, booleans, dates and ids passed as their own types are written verbatim (so -5 stays a number).
/// Dates are ISO 8601 UTC and numbers use the invariant culture.
/// </summary>
public sealed class CsvWriter : IAsyncDisposable
{
    public const string LineEnd = "\r\n";

    /// <summary>UTF-8 with a byte order mark, which makes Excel read the file as UTF-8.</summary>
    public static readonly Encoding Utf8WithBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    private readonly TextWriter _writer;

    public CsvWriter(Stream stream)
        : this(new StreamWriter(stream, Utf8WithBom, bufferSize: 16 * 1024, leaveOpen: true))
    {
    }

    public CsvWriter(TextWriter writer) => _writer = writer;

    public Task WriteRowAsync(params object?[] values) => _writer.WriteAsync(FormatRow(values));

    public Task FlushAsync() => _writer.FlushAsync();

    public async ValueTask DisposeAsync()
    {
        await _writer.FlushAsync();
        await _writer.DisposeAsync();
    }

    /// <summary>One record including its terminating CRLF.</summary>
    public static string FormatRow(IEnumerable<object?> values) =>
        string.Join(',', values.Select(FormatField)) + LineEnd;

    public static string FormatField(object? value) => value switch
    {
        null => "",
        string text => Quote(Neutralise(text)),
        char c => Quote(Neutralise(c.ToString())),
        bool b => b ? "true" : "false",
        DateTime dt => FormatDate(dt),
        DateTimeOffset dto => FormatDate(dto.UtcDateTime),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Guid g => g.ToString("D"),
        Enum e => Quote(e.ToString()),
        sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal =>
            ((IFormattable)value).ToString(null, CultureInfo.InvariantCulture),
        _ => Quote(Neutralise(Convert.ToString(value, CultureInfo.InvariantCulture) ?? "")),
    };

    /// <summary>Text starting with a formula character is prefixed with an apostrophe so spreadsheets treat it as text.</summary>
    public static string Neutralise(string text) =>
        text.Length > 0 && text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;

    private static string Quote(string text) =>
        text.AsSpan().IndexOfAny(",\"\r\n") >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;

    private static string FormatDate(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        var text = utc.ToString("yyyy-MM-dd'T'HH:mm:ss.FFF", CultureInfo.InvariantCulture);
        return text.TrimEnd('.') + "Z";
    }
}
