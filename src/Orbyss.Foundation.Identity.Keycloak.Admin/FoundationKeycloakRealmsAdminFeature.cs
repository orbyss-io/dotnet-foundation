using CShells;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Composes explicitly selected configured-realm initialization and settings administration.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Realms", DisplayName = "Keycloak Realm Administration",
    Description = "Initializes and updates declared realm settings without importing identity data.")]
public sealed class FoundationKeycloakRealmsAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        KeycloakAdminComposition.AddTransport(services, settings);
        services.TryAddSingleton<IKeycloakRealmAdministration, KeycloakRealmAdministration>();
    }
}
