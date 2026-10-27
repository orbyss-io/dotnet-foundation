using System.Text.Json.Serialization;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Uses a distinct output schema when read and write policies have different wire kinds.</summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
internal sealed record ResponseNumbers(
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] int? StrictCount,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] long? StrictSize,
    int? InheritedCount,
    long? InheritedSize,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] int RequiredCount,
    [property: JsonNumberHandling(JsonNumberHandling.Strict)] long RequiredSize,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] int? WriteOnlyCount,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)] long? BothSize
)
{
    /// <summary>Owns supported response metadata for the native result factory and schema producer.</summary>
    internal static JsonContractMetadata<ResponseNumbers> Contract { get; } = JsonContractMetadata<ResponseNumbers>.Response();
}
