using Orbyss.Foundation.Json.AspNetCore;

namespace Orbyss.Foundation.Json.Admission.Probe.Contracts;

/// <summary>Shared response meaning used by the change and read operations.</summary>
internal sealed record ValueResponse(
    Guid ValueId,
    string Label,
    long? Sequence,
    Guid ScopeId
)
{
    /// <summary>Declares supported response metadata once, independently of deployment byte limits.</summary>
    internal static JsonContractMetadata<ValueResponse> Contract { get; } = JsonContractMetadata<ValueResponse>.Response();
}
