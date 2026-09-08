using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions; using Orbyss.Foundation.Identity.Admin;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes portable scope and claim-mapping administration backed by Keycloak.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Scopes", DisplayName = "Keycloak Scope Administration", Description = "Manages reusable scopes, application scope assignments, and portable claim mappings.")]
public sealed class FoundationKeycloakScopesAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IIdentityScopeAdministration, KeycloakScopesAdministration>(); }
}
