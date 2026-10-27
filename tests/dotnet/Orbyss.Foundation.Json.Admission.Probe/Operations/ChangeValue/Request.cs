using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Operations.ChangeValue;

/// <summary>Owns the strict JSON input for changing one value.</summary>
internal sealed record Request(string Label, long? Sequence)
{
    /// <summary>Shares the exact definition between service registration and route metadata.</summary>
    internal static JsonContractMetadata<Request> Contract { get; } = JsonContractMetadata<Request>.Request();
}
