using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Orbyss.Foundation.Web.ProblemDetails;

/// <summary>Registers the common writer policy independently of optional global exception activation.</summary>
public static class FoundationProblemDetailsServiceCollectionExtensions
{
    /// <summary>Adds shared representation only; no global exception middleware or handler is activated.</summary>
    public static IServiceCollection AddFoundationProblemDetails(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // The native writer list is ordered. Admit the closed bounded shape before any default
        // writer, including when the host registered ASP.NET Problem Details before the shell.
        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IProblemDetailsWriter)
            && descriptor.ImplementationType == typeof(FoundationBoundedProblemDetailsWriter)))
            services.Insert(0, ServiceDescriptor.Singleton<IProblemDetailsWriter, FoundationBoundedProblemDetailsWriter>());
        services.AddOptions<FoundationProblemResponseOptions>();
        services.AddProblemDetails();
        services.TryAddSingleton<IProblemRepresentationPolicy, FoundationProblemRepresentationPolicy>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<ProblemDetailsOptions>,
            FoundationProblemDetailsOptionsSetup>());
        return services;
    }
}
