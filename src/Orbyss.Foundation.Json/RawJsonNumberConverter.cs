using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Json;

/// <summary>Preserves number lexemes without routine application property walking.</summary>
public sealed class RawJsonNumberConverter : JsonConverter<RawJsonNumber>
{
    /// <inheritdoc />
    public override RawJsonNumber Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.Number) throw new JsonException("Expected a number.");
        using var number = JsonDocument.ParseValue(ref reader);
        return new RawJsonNumber(number.RootElement.GetRawText());
    }
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, RawJsonNumber value, JsonSerializerOptions options)
    {
        if (value.Text is null) throw new JsonException("An uninitialized raw number is invalid.");
        writer.WriteRawValue(value.Text, skipInputValidation: false);
    }
}
