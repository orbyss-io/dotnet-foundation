using System.Text.Json.Serialization.Metadata;
using Microsoft.OpenApi;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Retains one agreed numeric shape from actual immutable serializer metadata.</summary>
internal sealed record JsonContractSchemaPlan(
    JsonTypeInfo TypeInfo,
    JsonContractDirection Direction,
    IReadOnlyDictionary<string, JsonNumericSchemaShape> NumericTypes
);
