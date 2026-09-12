using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Json;

/// <summary>Retains a validated numeric token exactly for product-owned legacy wire contracts.</summary>
[JsonConverter(typeof(RawJsonNumberConverter))]
public readonly record struct RawJsonNumber
{
    /// <summary>Validates one complete numeric token without normalizing it.</summary>
    public RawJsonNumber(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var reader = new Utf8JsonReader(bytes);
        if (!reader.Read() || reader.TokenType != JsonTokenType.Number || reader.TokenStartIndex != 0 ||
            reader.BytesConsumed != bytes.Length || reader.Read()) throw new JsonException("Expected one number token.");
        Text = text;
    }
    /// <summary>Gets exact token text. A default value is invalid for serialization.</summary>
    public string Text { get; }
}
