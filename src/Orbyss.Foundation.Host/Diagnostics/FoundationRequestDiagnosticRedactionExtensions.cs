using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Orbyss.Foundation.Host.Diagnostics;

/// <summary>Redacts native framework request failures while retaining their category, event and severity.</summary>
public static class FoundationRequestDiagnosticRedactionExtensions
{
    /// <summary>Decorates the host logger factory before startup; application logging remains its owner's policy.</summary>
    public static IServiceCollection AddFoundationRequestDiagnosticRedaction(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (services.Any(item => item.ServiceType == typeof(FoundationRequestDiagnosticRedactionRegistration))) return services;
        services.AddLogging();
        var original = services.Last(item => item.ServiceType == typeof(ILoggerFactory));
        if (original.Lifetime != ServiceLifetime.Singleton)
            throw new InvalidOperationException("The host logger factory must have singleton ownership.");
        services.Remove(original);
        services.AddSingleton(new FoundationRequestDiagnosticRedactionRegistration());
        services.AddSingleton<ILoggerFactory>(provider =>
        {
            var inner = original.ImplementationInstance as ILoggerFactory
                ?? original.ImplementationFactory?.Invoke(provider) as ILoggerFactory
                ?? (ILoggerFactory)ActivatorUtilities.CreateInstance(provider,
                    original.ImplementationType ?? throw new InvalidOperationException("Unsupported host logger factory registration."));
            return new FoundationRequestDiagnosticLoggerFactory(inner, ownsInner: original.ImplementationInstance is null);
        });
        return services;
    }
}
