using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions; using Orbyss.Foundation.Identity.Admin;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes portable user administration backed by Keycloak.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Users", DisplayName = "Keycloak User Administration", Description = "Creates, queries, updates, and deletes users through portable identity contracts.")]
public sealed class FoundationKeycloakUsersAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IIdentityUserAdministration, KeycloakUsersAdministration>(); }
}
