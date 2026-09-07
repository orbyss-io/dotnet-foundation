using Microsoft.Extensions.DependencyInjection;

namespace Orbyss.Foundation.Tasks;

/// <summary>Registers Orbyss Foundation task implementations with a shell service collection.</summary>
public static class TaskServiceCollectionExtensions
{
    /// <summary>Registers a scoped task that runs once during shell activation.</summary>
    /// <typeparam name="TTask">The startup-task implementation.</typeparam>
    /// <param name="services">The shell service collection.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundationStartupTask<TTask>(this IServiceCollection services)
        where TTask : class, IStartupTask
    {
        services.AddScoped<IStartupTask, TTask>();
        return services;
    }

    /// <summary>Registers a shell-singleton task that runs for the shell generation lifetime.</summary>
    /// <typeparam name="TTask">The background-task implementation.</typeparam>
    /// <param name="services">The shell service collection.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundationBackgroundTask<TTask>(this IServiceCollection services)
        where TTask : class, IBackgroundTask
    {
        services.AddSingleton<IBackgroundTask, TTask>();
        return services;
    }

    /// <summary>Registers a shell-singleton task that runs on a recurring interval.</summary>
    /// <typeparam name="TTask">The recurring-task implementation.</typeparam>
    /// <param name="services">The shell service collection.</param>
    /// <returns>The supplied service collection.</returns>
    public static IServiceCollection AddFoundationRecurringTask<TTask>(this IServiceCollection services)
        where TTask : class, IRecurringTask
    {
        services.AddSingleton<IRecurringTask, TTask>();
        return services;
    }
}
