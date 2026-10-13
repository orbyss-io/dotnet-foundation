using CShells;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Orbyss.Foundation.Execution;

namespace Orbyss.Foundation.PostgreSql;

/// <summary>Registers provider-owned policies and context mechanics through the consumer's provider feature.</summary>
public static class FoundationPostgreSqlServiceCollectionExtensions
{
    /// <summary>Binds one policy from Foundation:PostgreSql:Policies without starting application storage initialization.</summary>
    public static IServiceCollection AddFoundationPostgreSql<TContext>(this IServiceCollection services, ShellSettings settings,
        string policyName, Func<DbContextOptions<TContext>, TContext> construct) where TContext : FoundationPostgreSqlDbContext
        => services.AddFoundationPostgreSql(settings, policyName, construct, _ => []);

    /// <summary>Binds one owned policy and explicitly contributes ordered native interceptors from its tracked unit scope.</summary>
    /// <remarks>Resolve disposable contributions from DI. Transient contributions can be context-specific; scoped contributions share the unit. The retained scope owns disposal, not EF or individual contexts.</remarks>
    public static IServiceCollection AddFoundationPostgreSql<TContext>(this IServiceCollection services, ShellSettings settings,
        string policyName, Func<DbContextOptions<TContext>, TContext> construct,
        Func<IServiceProvider, IReadOnlyList<IInterceptor>> interceptors) where TContext : FoundationPostgreSqlDbContext
    {
        ArgumentNullException.ThrowIfNull(settings);
        var options = settings.GetConfigurationRoot().GetSection("Foundation:PostgreSql:Policies:" + policyName).Get<PostgreSqlOptions>()
            ?? throw new ArgumentException("The PostgreSQL policy requires typed configuration.");
        return services.AddFoundationPostgreSql(policyName, options, construct, interceptors);
    }

    /// <summary>Registers one stable keyed datasource, scoped nonpooled context factory and exact-generation lease adapter.</summary>
    public static IServiceCollection AddFoundationPostgreSql<TContext>(this IServiceCollection services,
        string policyName, PostgreSqlOptions options, Func<DbContextOptions<TContext>, TContext> construct) where TContext : FoundationPostgreSqlDbContext
        => services.AddFoundationPostgreSql(policyName, options, construct, _ => []);

    /// <summary>Registers a nonpooled native context and explicit ordered interceptor contributions without exposing provider or options replacement.</summary>
    /// <remarks>The callback runs once per admitted context with the tracked unit provider. Native interceptors are trusted application code and may change EF behavior; mandatory Foundation interceptors remain installed first.</remarks>
    public static IServiceCollection AddFoundationPostgreSql<TContext>(this IServiceCollection services,
        string policyName, PostgreSqlOptions options, Func<DbContextOptions<TContext>, TContext> construct,
        Func<IServiceProvider, IReadOnlyList<IInterceptor>> interceptors) where TContext : FoundationPostgreSqlDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(construct);
        ArgumentNullException.ThrowIfNull(interceptors);
        if (services.Any(descriptor => descriptor.ServiceType == typeof(PostgreSqlContextRegistration<TContext>)))
            throw new ArgumentException("A context type can select only one PostgreSQL policy per shell generation.");
        var policy = new PostgreSqlPolicy(options);
        services.AddFoundationExecution();
        var existing = services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(PostgreSqlPolicy)
            && descriptor.IsKeyedService && Equals(descriptor.ServiceKey, policyName));
        if (existing?.KeyedImplementationInstance is PostgreSqlPolicy selected && !selected.Matches(policy))
            throw new ArgumentException("Registrations sharing a PostgreSQL policy name must select identical native configuration.");
        if (existing is null)
        {
            services.AddKeyedSingleton(policyName, policy);
            services.AddKeyedSingleton<ShellPostgreSqlDataSource>(policyName, (provider, _) => new ShellPostgreSqlDataSource(policy,
                provider.GetService<ILoggerFactory>() ?? NullLoggerFactory.Instance));
        }
        services.AddSingleton(provider => new PostgreSqlContextRegistration<TContext>(
            provider.GetRequiredKeyedService<ShellPostgreSqlDataSource>(policyName), policy, construct, interceptors));
        services.AddScoped<ContextLeaseState<TContext>>();
        services.AddScoped<IDbContextFactory<TContext>, OwnedPostgreSqlDbContextFactory<TContext>>();
        services.AddSingleton<IPostgreSqlUnitLeaseFactory<TContext>, PostgreSqlUnitLeaseFactory<TContext>>();
        return services;
    }
}
