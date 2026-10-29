using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Orbyss.Foundation.Web.DynamicPages;

/// <summary>Registers consumer implementations without reflection-based configuration or a new Host.</summary>
public static class DynamicPageServiceCollectionExtensions
{
    /// <summary>Registers a resolver in the native request scope under one stable settings identity.</summary>
    public static IServiceCollection AddDynamicPageResolver<TResolver>(this IServiceCollection services, string id)
        where TResolver : class, IDynamicPageResolver
    {
        if (!DynamicPageCatalog.Identifier(id)) throw new ArgumentException("Invalid dynamic page resolver identity.", nameof(id));
        services.TryAddScoped<TResolver>();
        services.AddSingleton(new DynamicPageResolverRegistration(id, typeof(TResolver)));
        return services;
    }
}
