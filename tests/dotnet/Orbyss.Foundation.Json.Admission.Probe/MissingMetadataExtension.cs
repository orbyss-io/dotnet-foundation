using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Selects a trusted resolver that intentionally omits required message metadata.</summary>
public sealed class MissingMetadataExtension : IJsonProfileExtension, IJsonTypeInfoResolver
{
    /// <inheritdoc />
    public string Id => "admission-missing-metadata";
    /// <inheritdoc />
    public IReadOnlyList<JsonConverter> Converters => [];
    /// <inheritdoc />
    public IJsonTypeInfoResolver Resolver => this;
    /// <inheritdoc />
    public JsonTypeInfo? GetTypeInfo(Type type, JsonSerializerOptions options) => null;
}
