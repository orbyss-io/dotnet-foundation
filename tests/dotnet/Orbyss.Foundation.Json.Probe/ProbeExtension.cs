using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
namespace Orbyss.Foundation.Json.Probe;
/// <summary>Registers source-generated metadata under a stable code identity.</summary>
public sealed class ProbeExtension : IJsonProfileExtension
{
    /// <inheritdoc />
    public string Id => "probe";
    /// <inheritdoc />
    public IReadOnlyList<JsonConverter> Converters => [];
    /// <inheritdoc />
    public IJsonTypeInfoResolver? Resolver => ProbeContext.Default;
}
