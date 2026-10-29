using System.Text.Json.Serialization;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Separates named floating outputs, quoted finite decimals and read-only permissiveness.</summary>
internal sealed record NumericSpecialResponse(
    [property: JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)] double? Named,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString)] decimal? Quoted,
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] double? Plain,
    [property: JsonNumberHandling(JsonNumberHandling.WriteAsString | JsonNumberHandling.AllowNamedFloatingPointLiterals)] float? QuotedSpecial
)
{
    /// <summary>Owns the actual response-only number policy.</summary>
    internal static JsonContractMetadata<NumericSpecialResponse> Contract { get; } = JsonContractFactory.Response<NumericSpecialResponse>();
}
