using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Owns a deliberately nonstandard numeric wire representation for preservation tests.</summary>
internal sealed class CustomIntegerConverter : JsonConverter<int>
{
    /// <inheritdoc />
    public override int Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        var value = reader.GetString();
        if (value is null || !value.StartsWith("id:", StringComparison.Ordinal))
            throw new JsonException("Expected the owned identifier representation.");
        var digits = value.AsSpan(3);
        return int.Parse(
            digits,
            NumberStyles.None,
            CultureInfo.InvariantCulture
        );
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        int value,
        JsonSerializerOptions options
    )
    {
        var digits = value.ToString(CultureInfo.InvariantCulture);
        writer.WriteStringValue("id:" + digits);
    }
}
