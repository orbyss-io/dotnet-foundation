using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Proves ordinary same-type strict input and output continue sharing a schema.</summary>
internal sealed record StrictNumbers(int? Count, long? Size)
{
    /// <summary>Uses the actual profile's strict numeric default without attributes.</summary>
    internal static JsonContractMetadata<StrictNumbers> Request { get; } = JsonContractMetadata<StrictNumbers>.Request();
    /// <summary>Publishes the identical numeric wire shape in the response direction.</summary>
    internal static JsonContractMetadata<StrictNumbers> Response { get; } = JsonContractMetadata<StrictNumbers>.Response();
}
