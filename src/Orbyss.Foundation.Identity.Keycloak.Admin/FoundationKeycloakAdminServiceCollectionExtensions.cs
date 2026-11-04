using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orbyss.Foundation.Identity.Admin;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Composes reusable administration clients for workers and maintenance processes.</summary>
public static class FoundationKeycloakAdminServiceCollectionExtensions
{
    /// <summary>Names the shared administration HTTP client for explicit transport customization.</summary>
    public const string HttpClientName = "Orbyss.Foundation.Identity.Keycloak.Admin";

    /// <summary>Adds only the requested capabilities using service-account client credentials.</summary>
    public static IServiceCollection AddFoundationKeycloakAdministration(
        this IServiceCollection services,
        Action<KeycloakAdminOptions> configure,
        KeycloakAdminFeatures features,
        string environmentName = "Production")
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        const KeycloakAdminFeatures supported = (KeycloakAdminFeatures)4095;
        if (features == KeycloakAdminFeatures.None || (features & ~supported) != 0)
            throw new ArgumentOutOfRangeException(nameof(features), "Select supported administration capabilities explicitly.");
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName);
        services.Configure(configure);
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<KeycloakAdminOptions>>(
            new KeycloakAdminOptionsValidator(environmentName)));
        KeycloakAdminComposition.AddTransport(services);
        Add<IIdentityUserAdministration, KeycloakUsersAdministration>(services, features, KeycloakAdminFeatures.Users);
        Add<IIdentityApplicationAdministration, KeycloakApplicationsAdministration>(services, features, KeycloakAdminFeatures.Applications);
        Add<IIdentityScopeAdministration, KeycloakScopesAdministration>(services, features, KeycloakAdminFeatures.Scopes);
        Add<IIdentityAccessAdministration, KeycloakAccessAdministration>(services, features, KeycloakAdminFeatures.Access);
        Add<IIdentityEffectiveUserRoleReader, KeycloakAccessAdministration>(services, features, KeycloakAdminFeatures.Access);
        Add<IIdentityEnrollmentAdministration, KeycloakEnrollmentAdministration>(services, features, KeycloakAdminFeatures.Enrollment);
        Add<IIdentitySessionAdministration, KeycloakSessionsAdministration>(services, features, KeycloakAdminFeatures.Sessions);
        Add<IKeycloakAuthenticationFlowAdministration, KeycloakAuthenticationFlowAdministration>(services, features, KeycloakAdminFeatures.AuthenticationFlows);
        Add<IKeycloakIdentityProviderAdministration, KeycloakIdentityProviderAdministration>(services, features, KeycloakAdminFeatures.IdentityProviders);
        Add<IKeycloakOrganizationAdministration, KeycloakOrganizationAdministration>(services, features, KeycloakAdminFeatures.Organizations);
        Add<IKeycloakRealmOperations, KeycloakRealmOperations>(services, features, KeycloakAdminFeatures.RealmOperations);
        Add<IKeycloakRealmAdministration, KeycloakRealmAdministration>(services, features, KeycloakAdminFeatures.Realms);
        Add<IKeycloakConfigurationAdministration, KeycloakConfigurationAdministration>(services, features, KeycloakAdminFeatures.Configuration);
        return services;
    }

    /// <summary>Registers a selected contract without replacing a caller's explicit implementation.</summary>
    private static void Add<TContract, TImplementation>(IServiceCollection services, KeycloakAdminFeatures selected, KeycloakAdminFeatures feature)
        where TContract : class where TImplementation : class, TContract
    {
        if ((selected & feature) != 0) services.TryAddSingleton<TContract, TImplementation>();
    }
}
