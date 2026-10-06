using System.Text.Json;
using System.Text.Json.Serialization;
using Orbyss.Foundation.Collections.Core;

namespace Orbyss.Foundation.Json;

/// <summary>Typed array converter usable directly with generated contract metadata.</summary>
public sealed class ValueSequenceJsonConverter<T> : JsonConverter<ValueSequence<T>>
{
    /// <inheritdoc />
    public override bool HandleNull => true;
    /// <inheritdoc />
    public override ValueSequence<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Expected a nonnull sequence array.");
        var values = new List<T>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray) return new ValueSequence<T>(values);
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            if (value is null) throw new JsonException("Sequence items cannot be null.");
            values.Add(value);
        }
        throw new JsonException("Incomplete sequence array.");
    }
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, ValueSequence<T> value, JsonSerializerOptions options)
    {
        if (value is null) throw new JsonException("Sequence cannot be null.");
        writer.WriteStartArray();
        foreach (var item in value) JsonSerializer.Serialize(writer, item, options);
        writer.WriteEndArray();
    }
}
