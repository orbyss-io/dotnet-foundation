using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Orbyss.Foundation.Json;

/// <summary>Rejects converter overlap for root and nested types before metadata is used.</summary>
internal sealed class GuardedJsonTypeInfoResolver(IJsonTypeInfoResolver inner) : IJsonTypeInfoResolver
{
    /// <inheritdoc />
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options)
    {
        if (options.Converters.Count(converter => converter.CanConvert(type)) > 1)
            throw new InvalidOperationException("Foundation:Json has conflicting converters for a contract type.");
        return inner.GetTypeInfo(type, options);
    }
}
