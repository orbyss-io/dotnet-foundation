using CShells;
using Microsoft.EntityFrameworkCore;
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
    {
        ArgumentNullException.ThrowIfNull(settings);
        var options = settings.GetConfigurationRoot().GetSection("Foundation:PostgreSql:Policies:" + policyName).Get<PostgreSqlOptions>()
            ?? throw new ArgumentException("The PostgreSQL policy requires typed configuration.");
        return services.AddFoundationPostgreSql(policyName, options, construct);
    }

    /// <summary>Registers one stable keyed datasource, scoped nonpooled context factory and exact-generation lease adapter.</summary>
    public static IServiceCollection AddFoundationPostgreSql<TContext>(this IServiceCollection services,
        string policyName, PostgreSqlOptions options, Func<DbContextOptions<TContext>, TContext> construct) where TContext : FoundationPostgreSqlDbContext
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyName);
        ArgumentNullException.ThrowIfNull(construct);
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
            provider.GetRequiredKeyedService<ShellPostgreSqlDataSource>(policyName), policy, construct));
        services.AddScoped<ContextLeaseState<TContext>>();
        services.AddScoped<IDbContextFactory<TContext>, OwnedPostgreSqlDbContextFactory<TContext>>();
        services.AddSingleton<IPostgreSqlUnitLeaseFactory<TContext>, PostgreSqlUnitLeaseFactory<TContext>>();
        return services;
    }
}
