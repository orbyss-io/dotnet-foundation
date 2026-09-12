using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Orbyss.Foundation.Json.Probe;

/// <summary>Verifies overlapping code converters are rejected for nested contract types.</summary>
public sealed class ConflictingExtension : IJsonProfileExtension
{
    /// <inheritdoc />
    public string Id => "conflict";
    /// <inheritdoc />
    public IReadOnlyList<JsonConverter> Converters => [new RawJsonNumberConverter(), new RawJsonNumberConverter()];
    /// <inheritdoc />
    public IJsonTypeInfoResolver? Resolver => null;
}
