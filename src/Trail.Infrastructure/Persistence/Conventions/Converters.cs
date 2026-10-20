using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Trail.Infrastructure.Persistence.Conventions;

/// <summary>All timestamps are stored as UTC; MySQL drops the kind, so it is restored on read.</summary>
internal sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
    v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

internal sealed class NullableUtcDateTimeConverter() : ValueConverter<DateTime?, DateTime?>(
    v => v == null ? v : v.Value.Kind == DateTimeKind.Utc ? v : v.Value.ToUniversalTime(),
    v => v == null ? v : DateTime.SpecifyKind(v.Value, DateTimeKind.Utc));

/// <summary>
/// The Oracle MySQL provider has no native JSON mapping (no ToJson, no primitive collections), so structured
/// values that are never queried relationally are stored in a <c>json</c> column through System.Text.Json.
/// Anything that must be filtered, joined or indexed belongs in real columns instead.
/// </summary>
public static class JsonColumn
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static PropertyBuilder<T> HasJsonConversion<T>(this PropertyBuilder<T> builder)
    {
        builder.HasColumnType("json").HasConversion(
            new ValueConverter<T, string>(
                v => JsonSerializer.Serialize(v, Options),
                v => JsonSerializer.Deserialize<T>(v, Options)!),
            new ValueComparer<T>(
                (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                v => JsonSerializer.Serialize(v, Options).GetHashCode(StringComparison.Ordinal),
                v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!));
        return builder;
    }
}
