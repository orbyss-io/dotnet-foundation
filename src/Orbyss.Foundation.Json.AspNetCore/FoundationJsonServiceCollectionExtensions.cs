using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Registers explicit typed contracts with activation validation and public metadata.</summary>
public static class FoundationJsonServiceCollectionExtensions
{
    /// <summary>Registers one typed request reader, keeping deployment budgets in configured profiles.</summary>
    public static IServiceCollection AddJsonRequestContract<T>(
        this IServiceCollection services,
        JsonProfileKey profile,
        JsonProfileRequirement requirement
    )
    {
        var metadata = new JsonContractMetadata<T>(
            profile,
            JsonContractDirection.Request,
            requirement
        );
        return services.AddJsonContract(metadata);
    }
    /// <summary>Registers one typed admitted success-result factory.</summary>
    public static IServiceCollection AddJsonResponseContract<T>(
        this IServiceCollection services,
        JsonProfileKey profile,
        JsonProfileRequirement requirement
    )
    {
        var metadata = new JsonContractMetadata<T>(
            profile,
            JsonContractDirection.Response,
            requirement
        );
        return services.AddJsonContract(metadata);
    }

    /// <summary>Registers one operation-owned definition for runtime admission and OpenAPI; identical shared declarations are idempotent.</summary>
    public static IServiceCollection AddJsonContract<T>(this IServiceCollection services, JsonContractMetadata<T> contract)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(contract);
        ArgumentNullException.ThrowIfNull(contract.Requirement);
        if (string.IsNullOrEmpty(contract.Profile.Name) || !Enum.IsDefined(contract.Direction))
            throw new ArgumentException("The JSON contract has an invalid profile or direction.", nameof(contract));

        var registered = false;
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType != typeof(IJsonContractMetadata))
                continue;
            if (descriptor.ImplementationInstance is not IJsonContractMetadata existing)
                continue;
            if (existing.ContractType != typeof(T) || existing.Direction != contract.Direction)
                continue;
            JsonContractIdentity.RequireEquivalent(existing, contract);
            registered = true;
        }

        if (!registered)
            services.AddSingleton<IJsonContractMetadata>(contract);
        if (contract.Direction == JsonContractDirection.Request)
        {
            IJsonRequestReader<T> CreateReader(IServiceProvider provider)
            {
                var catalog = provider.GetRequiredService<JsonProfileCatalog>();
                return new JsonRequestReader<T>(catalog, contract);
            }

            services.TryAddSingleton<IJsonRequestReader<T>>(CreateReader);
        }
        else
        {
            IJsonResponseFactory<T> CreateResponseFactory(IServiceProvider provider)
            {
                var catalog = provider.GetRequiredService<JsonProfileCatalog>();
                return new JsonResponseFactory<T>(catalog, contract);
            }

            services.TryAddSingleton<IJsonResponseFactory<T>>(CreateResponseFactory);
        }

        return services;
    }

    /// <summary>Registers a request-scoped endpoint without resolving effect dependencies during metadata-only composition.</summary>
    public static IServiceCollection AddJsonEndpoint<TEndpoint>(this IServiceCollection services) where TEndpoint : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<TEndpoint>(CreateEndpoint<TEndpoint>);
        return services;
    }

    /// <summary>Defers constructor activation to the actual request scope.</summary>
    private static TEndpoint CreateEndpoint<TEndpoint>(IServiceProvider provider) where TEndpoint : class =>
        ActivatorUtilities.CreateInstance<TEndpoint>(provider);
}
