using System.Text.Json.Serialization;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Exercises property overrides, inherited class policy and independent request/read semantics.</summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
internal sealed record RequestNumbers(
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
    /// <summary>Owns the actual registered request profile.</summary>
    internal static JsonContractMetadata<RequestNumbers> Contract { get; } = JsonContractMetadata<RequestNumbers>.Request();
}
