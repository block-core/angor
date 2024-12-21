using System.Text.Json;
using System.Text.Json.Serialization;

namespace Angor.Shared.Utilities;

public class UnixDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number)
        {
            throw new JsonException("Expected a number for Unix timestamp.");
        }

        var unixTime = reader.GetInt64();
        return DateTimeOffset.FromUnixTimeSeconds(unixTime).UtcDateTime;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        // Ensure the DateTime is in UTC and has no milliseconds
        var utcValue = value.ToUniversalTime().AddTicks(-(value.Ticks % TimeSpan.TicksPerSecond));
        var unixTime = new DateTimeOffset(utcValue).ToUnixTimeSeconds();
        writer.WriteNumberValue(unixTime);
    }
}
