using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions; using Orbyss.Foundation.Identity.Admin;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes portable session administration backed by Keycloak.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Sessions", DisplayName = "Keycloak Session Administration", Description = "Observes and revokes user sessions through portable identity contracts.")]
public sealed class FoundationKeycloakSessionsAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IIdentitySessionAdministration, KeycloakSessionsAdministration>(); }
}
