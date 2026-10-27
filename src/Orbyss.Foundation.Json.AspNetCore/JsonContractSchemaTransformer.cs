using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Reconciles registered numeric schemas with actual profile metadata rather than unrelated global web JSON options.</summary>
public sealed class JsonContractSchemaTransformer : IOpenApiSchemaTransformer
{
    /// <summary>Retains immutable plans for explicitly registered contracts and their reachable object types.</summary>
    private readonly IReadOnlyDictionary<Type, JsonContractSchemaPlan> plans;

    /// <summary>Admits compatible shared schema ownership without resolving request handlers or effect providers.</summary>
    public JsonContractSchemaTransformer(JsonProfileCatalog profiles, IEnumerable<IJsonContractMetadata> contracts)
    {
        plans = CreatePlans(profiles, contracts);
    }

    /// <inheritdoc />
    public Task TransformAsync(
        OpenApiSchema schema,
        OpenApiSchemaTransformerContext context,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (plans.TryGetValue(context.JsonTypeInfo.Type, out var plan)
            && (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object || context.JsonPropertyInfo is null))
            JsonNumericSchemaPolicy.Apply(schema, plan);
        return Task.CompletedTask;
    }

    /// <summary>Collects actual serializer metadata and rejects incompatible uses of one shared schema identity.</summary>
    private static IReadOnlyDictionary<Type, JsonContractSchemaPlan> CreatePlans(
        JsonProfileCatalog profiles,
        IEnumerable<IJsonContractMetadata> contracts
    )
    {
        var result = new Dictionary<Type, JsonContractSchemaPlan>();
        foreach (var contract in contracts)
        {
            var profile = profiles.Get(contract.Profile);
            var visited = new HashSet<Type>();
            Collect(
                profile,
                contract.ContractType,
                contract.Direction,
                true,
                visited,
                result
            );
        }

        return result;
    }

    /// <summary>Walks a finite serializer metadata graph, not an assembly or arbitrary runtime object graph.</summary>
    private static void Collect(
        JsonProfile profile,
        Type type,
        JsonContractDirection direction,
        bool root,
        ISet<Type> visited,
        IDictionary<Type, JsonContractSchemaPlan> result
    )
    {
        if (!visited.Add(type))
            return;
        var info = profile.TypeInfo(type);
        if (root || info.Kind == JsonTypeInfoKind.Object)
        {
            var kinds = JsonNumericSchemaPolicy.Describe(info, direction);
            if (kinds.Count != 0)
            {
                var plan = new JsonContractSchemaPlan(
                    info,
                    direction,
                    kinds
                );
                if (result.TryGetValue(type, out var existing) && !Equivalent(existing, plan))
                    throw new InvalidOperationException("Registered JSON contracts require incompatible numeric schemas for a shared CLR type. Use distinct wire types or align their numeric policies.");
                result.TryAdd(type, plan);
            }
        }

        if (info.Kind == JsonTypeInfoKind.Object)
        {
            foreach (var property in info.Properties)
            {
                if (property.CustomConverter is null && !property.IsExtensionData)
                    Collect(
                        profile,
                        property.PropertyType,
                        direction,
                        false,
                        visited,
                        result
                    );
            }
        }
        if (info.ElementType is { } element)
            Collect(
                profile,
                element,
                direction,
                false,
                visited,
                result
            );
        if (info.PolymorphismOptions is { } polymorphism)
        {
            foreach (var derived in polymorphism.DerivedTypes)
                Collect(
                    profile,
                    derived.DerivedType,
                    direction,
                    false,
                    visited,
                    result
                );
        }
    }

    /// <summary>Requires the same numeric property paths and admitted JSON kinds for shared schema use.</summary>
    private static bool Equivalent(JsonContractSchemaPlan first, JsonContractSchemaPlan second)
    {
        if (first.NumericTypes.Count != second.NumericTypes.Count)
            return false;
        foreach (var property in first.NumericTypes)
        {
            if (!second.NumericTypes.TryGetValue(property.Key, out var kinds) || kinds != property.Value)
                return false;
        }

        return true;
    }
}
