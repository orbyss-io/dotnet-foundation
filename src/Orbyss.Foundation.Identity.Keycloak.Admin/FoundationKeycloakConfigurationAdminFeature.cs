using CShells;
using CShells.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Orbyss.Foundation.Identity.Keycloak.Admin;

/// <summary>Composes explicitly selected provider configuration reads and partial writes.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Configuration", DisplayName = "Keycloak Configuration Administration",
    Description = "Reads and updates declared client, scope, mapper and role settings without credential imports.")]
public sealed class FoundationKeycloakConfigurationAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services)
    {
        KeycloakAdminComposition.AddTransport(services, settings);
        services.TryAddSingleton<IKeycloakConfigurationAdministration, KeycloakConfigurationAdministration>();
        services.TryAddSingleton<IKeycloakConfigurationRemoval, KeycloakConfigurationRemoval>();
    }
}
