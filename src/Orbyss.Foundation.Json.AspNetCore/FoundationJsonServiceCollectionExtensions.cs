using Microsoft.Extensions.DependencyInjection;

namespace Orbyss.Foundation.Json.AspNetCore;

/// <summary>Registers explicit typed contracts with activation validation and public metadata.</summary>
public static class FoundationJsonServiceCollectionExtensions
{
    /// <summary>Registers one typed request reader, keeping deployment budgets in configured profiles.</summary>
    public static IServiceCollection AddJsonRequestContract<T>(this IServiceCollection services, JsonProfileKey profile, JsonProfileRequirement requirement)
    {
        var metadata = new JsonContractMetadata<T>(profile, JsonContractDirection.Request, requirement);
        services.AddSingleton<IJsonContractMetadata>(metadata);
        services.AddSingleton<IJsonRequestReader<T>>(provider => new JsonRequestReader<T>(provider.GetRequiredService<JsonProfileCatalog>(), metadata));
        return services;
    }
    /// <summary>Registers one typed admitted success-result factory.</summary>
    public static IServiceCollection AddJsonResponseContract<T>(this IServiceCollection services, JsonProfileKey profile, JsonProfileRequirement requirement)
    {
        var metadata = new JsonContractMetadata<T>(profile, JsonContractDirection.Response, requirement);
        services.AddSingleton<IJsonContractMetadata>(metadata);
        services.AddSingleton<IJsonResponseFactory<T>>(provider => new JsonResponseFactory<T>(provider.GetRequiredService<JsonProfileCatalog>(), metadata));
        return services;
    }
}
