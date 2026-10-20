namespace Trail.Application.Common;

public static class StringExtensions
{
    /// <summary>Returns null for blank text, so optional fields never store empty strings.</summary>
    public static string? NullIfEmpty(this string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
