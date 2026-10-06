using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Orbyss.Foundation.Execution.Core;

namespace Orbyss.Foundation.Execution;

/// <summary>Registers a replaceable clock and deadline factory without process-global configuration.</summary>
public static class FoundationExecutionServiceCollectionExtensions
{
    /// <summary>Adds the owned TimeProvider deadline implementation to a provider.</summary>
    public static IServiceCollection AddFoundationExecution(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IExecutionDeadlineFactory, TimeProviderDeadlineFactory>();
        return services;
    }
}
