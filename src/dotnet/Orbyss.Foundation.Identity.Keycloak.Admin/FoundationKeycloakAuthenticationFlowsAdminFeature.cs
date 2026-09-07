using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes Keycloak-specific authentication-flow administration.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.AuthenticationFlows", DisplayName = "Keycloak Authentication Flow Administration", Description = "Manages Keycloak authentication flows and executions.")]
public sealed class FoundationKeycloakAuthenticationFlowsAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IKeycloakAuthenticationFlowAdministration, KeycloakAuthenticationFlowAdministration>(); }
}
