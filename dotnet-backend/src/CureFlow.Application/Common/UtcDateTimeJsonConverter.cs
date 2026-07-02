using System.Text.Json;
using System.Text.Json.Serialization;

namespace CureFlow.Application.Common;

/// <summary>
/// Deserializes JSON date strings as UTC (Kind=Utc) for PostgreSQL timestamptz compatibility.
/// </summary>
public sealed class UtcDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        DateTimeHelper.EnsureUtc(reader.GetDateTime());

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
        writer.WriteStringValue(DateTimeHelper.EnsureUtc(value));
}

public sealed class UtcNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        return DateTimeHelper.EnsureUtc(reader.GetDateTime());
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }
        writer.WriteStringValue(DateTimeHelper.EnsureUtc(value.Value));
    }
}
