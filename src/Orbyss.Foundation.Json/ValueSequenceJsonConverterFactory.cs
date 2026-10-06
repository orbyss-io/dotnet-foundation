using System.Text.Json;
using System.Text.Json.Serialization;
using Orbyss.Foundation.Collections.Core;

namespace Orbyss.Foundation.Json;

/// <summary>Creates array adapters only for the owned collection type.</summary>
internal sealed class ValueSequenceJsonConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType
        && typeToConvert.GetGenericTypeDefinition() == typeof(ValueSequence<>);
    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(ValueSequenceJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()))!;
}
