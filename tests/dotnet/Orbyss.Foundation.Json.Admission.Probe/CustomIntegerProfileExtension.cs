using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Orbyss.Foundation.Json.Admission.Probe;

/// <summary>Selects the custom converter only for an explicit profile.</summary>
internal sealed class CustomIntegerProfileExtension : IJsonProfileExtension
{
    /// <inheritdoc />
    public string Id => "custom-integer";
    /// <inheritdoc />
    public IReadOnlyList<JsonConverter> Converters => new JsonConverter[] { new CustomIntegerConverter() };
    /// <inheritdoc />
    public IJsonTypeInfoResolver? Resolver => null;
}
