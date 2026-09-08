using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes Keycloak-specific organization administration.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Organizations", DisplayName = "Keycloak Organization Administration", Description = "Manages Keycloak organizations and memberships.")]
public sealed class FoundationKeycloakOrganizationsAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IKeycloakOrganizationAdministration, KeycloakOrganizationAdministration>(); }
}
