using System.Buffers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Json;

/// <summary>Admits each native string/key before Utf8JsonWriter can reserve and encode an oversized scalar.</summary>
internal sealed class BoundedStringJsonConverter(int maximumBytes) : JsonConverter<string>
{
    /// <inheritdoc />
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? reader.GetString()! : throw new JsonException("Expected a string.");
    /// <inheritdoc />
    public override string ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => reader.GetString()!;
    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        Admit(writer, value, options.Encoder ?? JavaScriptEncoder.Default, punctuationBytes: 2);
        writer.WriteStringValue(value);
    }
    /// <inheritdoc />
    public override void WriteAsPropertyName(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        Admit(writer, value, options.Encoder ?? JavaScriptEncoder.Default, punctuationBytes: 3);
        writer.WritePropertyName(value);
    }
    /// <summary>Counts native encoded scalar bytes with fixed scratch storage before retaining its output.</summary>
    private void Admit(Utf8JsonWriter writer, string value, JavaScriptEncoder encoder, int punctuationBytes)
    {
        var remaining = maximumBytes - writer.BytesCommitted - writer.BytesPending - punctuationBytes;
        var text = value.AsSpan();
        Span<byte> scalar = stackalloc byte[4];
        Span<byte> escaped = stackalloc byte[32];
        while (!text.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(text, out var rune, out var consumed) != OperationStatus.Done)
                throw new JsonResponseContractException(JsonFailureCodes.ResponseInvalidUnicode);
            var count = rune.EncodeToUtf8(scalar);
            if (encoder.WillEncode(rune.Value))
            {
                if (encoder.EncodeUtf8(scalar[..count], escaped, out _, out var escapedBytes) != OperationStatus.Done)
                    throw new JsonResponseContractException(JsonFailureCodes.ResponseInvalidContract);
                count = escapedBytes;
            }
            remaining -= count;
            if (remaining < 0) throw new JsonOutputLimitException();
            text = text[consumed..];
        }
        if (remaining < 0) throw new JsonOutputLimitException();
    }
}
