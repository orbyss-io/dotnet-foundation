using System.Text.Json.Serialization;
using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Allows the finite named-floating set without accepting ordinary quoted numbers.</summary>
internal sealed record NumericNamedRequest(
    [property: JsonNumberHandling(JsonNumberHandling.AllowNamedFloatingPointLiterals)] double? Value
)
{
    /// <summary>Declares a request whose string branch is the named-literal enum.</summary>
    internal static JsonContractMetadata<NumericNamedRequest> Contract { get; } = JsonContractFactory.Request<NumericNamedRequest>();
}
