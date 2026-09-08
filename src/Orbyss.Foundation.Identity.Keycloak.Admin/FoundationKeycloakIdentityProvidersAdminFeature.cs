using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes Keycloak-specific identity-provider administration.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.IdentityProviders", DisplayName = "Keycloak Identity Provider Administration", Description = "Manages external identity providers and their mappers.")]
public sealed class FoundationKeycloakIdentityProvidersAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IKeycloakIdentityProviderAdministration, KeycloakIdentityProviderAdministration>(); }
}
