using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Orbyss.Foundation.DomainEvents;

/// <summary>Registers the default awaited in-process domain-event dispatcher.</summary>
public static class FoundationDomainEventServiceCollectionExtensions
{
    /// <summary>Registers the dispatcher and validates its bounded-publication options.</summary>
    /// <param name="services">The current shell service collection.</param>
    /// <param name="configure">Optionally configures dispatch safety bounds.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundationDomainEvents(
        this IServiceCollection services,
        Action<DomainEventDispatchOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = new DomainEventDispatchOptions();
        configure?.Invoke(options);
        options.Validate();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(options);
        services.TryAddScoped<IDomainEventPublisher, DefaultDomainEventPublisher>();
        return services;
    }
}
