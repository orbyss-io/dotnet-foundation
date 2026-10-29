using System.Text.Json.Serialization;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Exercises built-in signed, unsigned, decimal and floating numeric families.</summary>
[JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
internal sealed record NumericFamily(
    byte? Byte,
    sbyte? SignedByte,
    short? Small,
    ushort? UnsignedSmall,
    uint? Unsigned,
    ulong? UnsignedLong,
    Int128? Wide,
    UInt128? UnsignedWide,
    decimal? Price,
    float? Single,
    double? Ratio,
    Half? Half
)
{
    /// <summary>Declares one request-only wire shape so read permissiveness cannot broaden output.</summary>
    internal static JsonContractMetadata<NumericFamily> Contract { get; } = JsonContractFactory.Request<NumericFamily>();
}
