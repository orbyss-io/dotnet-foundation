using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Orbyss.Foundation.Json;

/// <summary>Supplies stateless code-owned converters or a metadata resolver under an allowlisted identity.</summary>
public interface IJsonProfileExtension
{
    /// <summary>Gets the unique configuration identity.</summary>
    string Id { get; }
    /// <summary>Gets converters to install. Overlap is rejected for each resolved type.</summary>
    IReadOnlyList<JsonConverter> Converters { get; }
    /// <summary>Gets optional source-generated or custom metadata. Select at most one resolver per profile.</summary>
    IJsonTypeInfoResolver? Resolver { get; }
}
