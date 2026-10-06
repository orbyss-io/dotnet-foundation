using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Orbyss.Foundation.Collections.Core;

namespace Orbyss.Foundation.Json;

/// <summary>Allowlisted JSON array adaptation for owned collection values.</summary>
public sealed class ValueSequenceJsonExtension : IJsonProfileExtension
{
    /// <summary>Names this code-owned extension for profile configuration.</summary>
    public const string ExtensionId = "value-sequence-v1";
    /// <inheritdoc />
    public string Id => ExtensionId;
    /// <inheritdoc />
    public IReadOnlyList<JsonConverter> Converters { get; } = [new ValueSequenceJsonConverterFactory()];
    /// <inheritdoc />
    public IJsonTypeInfoResolver? Resolver => null;
}
