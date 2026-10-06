using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Orbyss.Foundation.Json;

/// <summary>Supplies only generated contract metadata while collection encoding remains the JSON adapter's responsibility.</summary>
internal sealed class HolderJsonExtension : IJsonProfileExtension
{
    /// <inheritdoc />
    public string Id => "holder-generated-v1";
    /// <inheritdoc />
    public IReadOnlyList<JsonConverter> Converters => [];
    /// <inheritdoc />
    public IJsonTypeInfoResolver? Resolver => HolderContext.Default;
}
