using CShells; using CShells.Features; using Microsoft.Extensions.DependencyInjection; using Microsoft.Extensions.DependencyInjection.Extensions; using Orbyss.Foundation.Identity.Admin;
namespace Orbyss.Foundation.Identity.Keycloak.Admin;
/// <summary>Composes portable credential and enrollment administration backed by Keycloak.</summary>
[ShellFeature(name: "Orbyss.Foundation.Identity.Keycloak.Admin.Enrollment", DisplayName = "Keycloak Enrollment Administration", Description = "Manages credentials and sends required-action enrollment messages.")]
public sealed class FoundationKeycloakEnrollmentAdminFeature(ShellSettings settings) : IShellFeature
{
    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services) { KeycloakAdminComposition.AddTransport(services, settings); services.TryAddSingleton<IIdentityEnrollmentAdministration, KeycloakEnrollmentAdministration>(); }
}
