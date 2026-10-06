using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Exports the same typed requirements used for activation and endpoint admission.</summary>
public sealed class JsonContractOperationTransformer : IOpenApiOperationTransformer
{
    /// <inheritdoc />
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var contracts = context.Description.ActionDescriptor.EndpointMetadata.OfType<IJsonContractMetadata>().ToArray();
        if (contracts.Length == 0) return Task.CompletedTask;
        var descriptions = new JsonArray();
        foreach (var contract in contracts)
            descriptions.Add(new JsonObject
            {
                ["direction"] = contract.Direction.ToString().ToLowerInvariant(), ["profile"] = contract.Profile.Name,
                ["preset"] = contract.Requirement.Preset, ["minimumBytes"] = contract.Requirement.MinimumBytes,
                ["maximumBytes"] = contract.Requirement.MaximumBytes, ["minimumDepth"] = contract.Requirement.MinimumDepth,
                ["maximumDepth"] = contract.Requirement.MaximumDepth
            });
        operation.Extensions ??= new Dictionary<string, IOpenApiExtension>();
        operation.Extensions["x-foundation-json-contracts"] = new JsonNodeExtension(descriptions);
        return Task.CompletedTask;
    }
}
